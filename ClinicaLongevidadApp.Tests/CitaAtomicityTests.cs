using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ClinicaLongevidadApp.Tests;

public sealed class CitaAtomicityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cita-atomic-" + Guid.NewGuid().ToString("N"));
    private readonly Keys keys = new();
    private readonly string? previousRole = Sesion.RolActual;
    private string DbPath => Path.Combine(root, "synthetic.db");
    private string ConnectionString => new SqliteConnectionStringBuilder
        { DataSource = DbPath, Pooling = false, ForeignKeys = true }.ToString();
    private readonly AuditoriaService audit;

    private sealed class Keys : IKeyProvider
    {
        public bool Available = true;
        private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
        public byte[] GetHmacKey() => Available ? key : Array.Empty<byte>();
        public string GetHmacKeyVersion() => "appointment-test-v1";
        public byte[]? GetHmacKeyByVersion(string? version) => version == GetHmacKeyVersion() ? key : null;
        public byte[]? GetEncryptionKey() => null;
        public string GetEncryptionKeyVersion() => "";
        public byte[]? GetEncryptionKeyByVersion(string? version) => null;
    }

    public CitaAtomicityTests()
    {
        Directory.CreateDirectory(root);
        using (var schema = new SQLite.SQLiteConnection(DbPath)) schema.CreateTable<Cita>();
        audit = new AuditoriaService(ConnectionString, keys);
        Assert.True(audit.IsInitialized);
        Sesion.RolActual = "Recepcion";
    }

    public void Dispose() => Sesion.RolActual = previousRole;

    private object? Sql(string sql)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static Cita NewAppointment() => new()
    {
        PacienteId = 123, PacienteNombre = "Paciente sintético", Fecha = new DateTime(2026, 10, 1),
        Hora = "09:30", Profesional = "Profesional sintético", Estado = "Pendiente",
        FechaCreacion = new DateTime(2000, 1, 1)
    };

    private Cita Read(int id)
    {
        using var connection = new SQLite.SQLiteConnection(DbPath);
        return connection.Find<Cita>(id);
    }

    [Fact]
    public void SuccessfulOperationsPreserveFieldsAndCreateOneAuditEach()
    {
        var cita = NewAppointment();
        var start = DateTime.Now;
        CitaService.Guardar(cita, audit, DbPath);
        Assert.True(cita.Id > 0);
        Assert.InRange(cita.FechaCreacion, start, DateTime.Now);
        var createdAt = cita.FechaCreacion;
        foreach (var state in new[] { "Pendiente", "Confirmada", "Sala espera", "En consulta", "Finalizada", "Facturada", "Cancelada" })
        {
            cita.Estado = state;
            cita.Hora = "11:45";
            CitaService.Guardar(cita, audit, DbPath);
            foreach (var property in typeof(Cita).GetProperties())
                Assert.Equal(property.GetValue(cita), property.GetValue(Read(cita.Id)));
            Assert.Equal(createdAt, cita.FechaCreacion);
        }
        Assert.Equal(8L, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Equal(1L, Sql("SELECT COUNT(*) FROM Auditoria WHERE Accion='Cita.Crear'"));
        Assert.Equal(7L, Sql("SELECT COUNT(*) FROM Auditoria WHERE Accion='Cita.Actualizar'"));
        Assert.Empty(audit.VerifyIntegrity());
        using var payload = JsonDocument.Parse(audit.GetRecentAudits(1)[0].Detalles!);
        Assert.Equal(cita.Id.ToString(), payload.RootElement.GetProperty("Id").GetString());
        Assert.Equal("Cancelada", payload.RootElement.GetProperty("Estado").GetString());
    }

    [Theory]
    [InlineData(false, "audit-insert")]
    [InlineData(true, "audit-insert")]
    [InlineData(false, "key")]
    [InlineData(true, "key")]
    [InlineData(false, "commit")]
    [InlineData(true, "commit")]
    [InlineData(false, "business")]
    [InlineData(true, "business")]
    public void FailureRollsBackBusinessAndAudit(bool update, string failure)
    {
        var cita = NewAppointment();
        if (update) CitaService.Guardar(cita, audit, DbPath);
        var originalJson = update ? JsonSerializer.Serialize(Read(cita.Id)) : null;
        var originalId = cita.Id;
        var createdAt = cita.FechaCreacion;
        var auditCount = Sql("SELECT COUNT(*) FROM Auditoria");
        if (failure == "key") keys.Available = false;
        else if (failure == "audit-insert")
            Sql("CREATE TRIGGER reject_audit BEFORE INSERT ON Auditoria BEGIN SELECT RAISE(ABORT,'synthetic audit failure'); END;");
        else if (failure == "business")
            Sql($"CREATE TRIGGER reject_business BEFORE {(update ? "UPDATE" : "INSERT")} ON Cita BEGIN SELECT RAISE(ABORT,'synthetic business failure'); END;");
        else
            Sql("CREATE TABLE Parent (Id INTEGER PRIMARY KEY); CREATE TABLE DeferredFailure (Id INTEGER REFERENCES Parent(Id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER reject_commit AFTER INSERT ON Auditoria BEGIN INSERT INTO DeferredFailure VALUES (123); END;");
        cita.Estado = "Confirmada";
        cita.Hora = "12:00";
        var error = Record.Exception(() => CitaService.Guardar(cita, audit, DbPath));
        if (failure == "key") Assert.IsType<InvalidOperationException>(error);
        else Assert.IsType<SqliteException>(error);
        Assert.Equal(auditCount, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Equal(update ? 1L : 0L, Sql("SELECT COUNT(*) FROM Cita"));
        Assert.Equal(originalId, cita.Id);
        Assert.Equal(createdAt, cita.FechaCreacion);
        if (update) Assert.Equal(originalJson, JsonSerializer.Serialize(Read(originalId)));
        if (failure == "commit") Assert.Equal(0L, Sql("SELECT COUNT(*) FROM DeferredFailure"));
    }

    [Fact]
    public void MissingAuditDifferentDatabaseAndMissingAppointmentRejectChanges()
    {
        Assert.Throws<InvalidOperationException>(() => CitaService.Guardar(NewAppointment(), null, DbPath));
        var other = new AuditoriaService(new SqliteConnectionStringBuilder
            { DataSource = Path.Combine(root, "other.db"), Pooling = false }.ToString(), keys);
        Assert.Throws<InvalidOperationException>(() => CitaService.Guardar(NewAppointment(), other, DbPath));
        var missing = NewAppointment();
        missing.Id = 999;
        Assert.Throws<InvalidOperationException>(() => CitaService.Guardar(missing, audit, DbPath));
        Assert.Equal(0L, Sql("SELECT COUNT(*) FROM Cita"));
        Assert.Empty(audit.GetRecentAudits(10));
        Assert.Empty(other.GetRecentAudits(10));
    }
}
