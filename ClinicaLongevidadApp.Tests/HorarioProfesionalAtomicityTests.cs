using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ClinicaLongevidadApp.Tests;

public sealed class HorarioProfesionalAtomicityTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "horario-atomic-" + Guid.NewGuid().ToString("N"));
    private readonly Keys keys = new();
    private string DbPath => Path.Combine(root, "synthetic.db");
    private string ConnectionString => new SqliteConnectionStringBuilder
        { DataSource = DbPath, Pooling = false, ForeignKeys = true }.ToString();
    private readonly AuditoriaService audit;

    private sealed class Keys : IKeyProvider
    {
        public bool Available = true;
        private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
        public byte[] GetHmacKey() => Available ? key : Array.Empty<byte>();
        public string GetHmacKeyVersion() => "schedule-test-v1";
        public byte[]? GetHmacKeyByVersion(string? version) => version == GetHmacKeyVersion() ? key : null;
        public byte[]? GetEncryptionKey() => null;
        public string GetEncryptionKeyVersion() => "";
        public byte[]? GetEncryptionKeyByVersion(string? version) => null;
    }

    public HorarioProfesionalAtomicityTests()
    {
        Directory.CreateDirectory(root);
        using (var schema = new SQLite.SQLiteConnection(DbPath)) schema.CreateTable<HorarioProfesional>();
        audit = new AuditoriaService(ConnectionString, keys);
        Assert.True(audit.IsInitialized);
    }

    private object? Sql(string sql)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static HorarioProfesional NewSchedule() => new()
    {
        Profesional = "Profesional sintético", DiaSemana = 1, HoraInicio = "08:15",
        HoraFin = "14:45", IntervaloMinutos = 15, Activo = true
    };

    private HorarioProfesional Read(int id)
    {
        using var connection = new SQLite.SQLiteConnection(DbPath);
        return connection.Find<HorarioProfesional>(id);
    }

    private void AssertEquivalent(HorarioProfesional expected)
    {
        var actual = Read(expected.Id);
        foreach (var property in typeof(HorarioProfesional).GetProperties())
            Assert.Equal(property.GetValue(expected), property.GetValue(actual));
        using var payload = JsonDocument.Parse(audit.GetRecentAudits(1)[0].Detalles!);
        foreach (var property in typeof(HorarioProfesional).GetProperties())
            Assert.Equal(property.GetValue(expected)?.ToString(), payload.RootElement.GetProperty(property.Name).GetString());
    }

    [Fact]
    public void SuccessPreservesAllFieldsAndAuditsExactlyOncePerSave()
    {
        var horario = NewSchedule();
        HorarioProfesionalService.Guardar(horario, audit, DbPath);
        Assert.True(horario.Id > 0);
        AssertEquivalent(horario);
        horario.Profesional = "Otro profesional";
        horario.DiaSemana = 5;
        horario.HoraInicio = "16:00";
        horario.HoraFin = "19:30";
        horario.IntervaloMinutos = 30;
        horario.Activo = false;
        HorarioProfesionalService.Guardar(horario, audit, DbPath);
        AssertEquivalent(horario);
        Assert.Equal(1L, Sql("SELECT COUNT(*) FROM HorarioProfesional"));
        Assert.Equal(2L, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Equal("HorarioProfesional.Crear|HorarioProfesional.Actualizar",
            Sql("SELECT group_concat(Accion,'|') FROM (SELECT Accion FROM Auditoria ORDER BY Id)"));
        Assert.Empty(audit.VerifyIntegrity());
    }

    [Theory]
    [InlineData(false, "audit")]
    [InlineData(true, "audit")]
    [InlineData(false, "key")]
    [InlineData(true, "key")]
    [InlineData(false, "commit")]
    [InlineData(true, "commit")]
    [InlineData(false, "business")]
    [InlineData(true, "business")]
    public void FailureRollsBackBusinessAndAudit(bool update, string failure)
    {
        var horario = NewSchedule();
        if (update) HorarioProfesionalService.Guardar(horario, audit, DbPath);
        var original = update ? JsonSerializer.Serialize(Read(horario.Id)) : null;
        var originalId = horario.Id;
        var auditCount = Sql("SELECT COUNT(*) FROM Auditoria");
        if (failure == "key") keys.Available = false;
        else if (failure == "audit")
            Sql("CREATE TRIGGER reject_audit BEFORE INSERT ON Auditoria BEGIN SELECT RAISE(ABORT,'synthetic audit failure'); END;");
        else if (failure == "business")
            Sql($"CREATE TRIGGER reject_business BEFORE {(update ? "UPDATE" : "INSERT")} ON HorarioProfesional BEGIN SELECT RAISE(ABORT,'synthetic business failure'); END;");
        else
            Sql("CREATE TABLE Parent (Id INTEGER PRIMARY KEY); CREATE TABLE DeferredFailure (Id INTEGER REFERENCES Parent(Id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER reject_commit AFTER INSERT ON Auditoria BEGIN INSERT INTO DeferredFailure VALUES (123); END;");
        horario.Profesional = "Must not persist";
        horario.HoraInicio = "12:00";
        horario.Activo = false;
        var error = Record.Exception(() => HorarioProfesionalService.Guardar(horario, audit, DbPath));
        if (failure == "key") Assert.IsType<InvalidOperationException>(error);
        else Assert.IsType<SqliteException>(error);
        Assert.Equal(auditCount, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Equal(update ? 1L : 0L, Sql("SELECT COUNT(*) FROM HorarioProfesional"));
        Assert.Equal(originalId, horario.Id);
        if (update) Assert.Equal(original, JsonSerializer.Serialize(Read(originalId)));
        if (failure == "commit") Assert.Equal(0L, Sql("SELECT COUNT(*) FROM DeferredFailure"));
    }

    [Theory]
    [InlineData("missing-audit")]
    [InlineData("different-database")]
    [InlineData("missing-schedule")]
    public void InvalidContextRejectsChanges(string scenario)
    {
        AuditoriaService? service = scenario == "missing-audit" ? null : scenario == "different-database"
            ? new AuditoriaService(new SqliteConnectionStringBuilder
                { DataSource = Path.Combine(root, "other.db"), Pooling = false }.ToString(), keys)
            : audit;
        var horario = NewSchedule();
        horario.Id = 999;
        Assert.Throws<InvalidOperationException>(() => HorarioProfesionalService.Guardar(horario, service, DbPath));
        if (scenario != "missing-schedule")
            Assert.Throws<InvalidOperationException>(() => HorarioProfesionalService.Guardar(NewSchedule(), service, DbPath));
        Assert.Equal(0L, Sql("SELECT COUNT(*) FROM HorarioProfesional"));
        Assert.Empty(audit.GetRecentAudits(10));
        if (service != null) Assert.Empty(service.GetRecentAudits(10));
    }
}
