using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ClinicaLongevidadApp.Tests;

public sealed class PacienteAtomicityTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "paciente-atomic-" + Guid.NewGuid().ToString("N"));
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
        public string GetHmacKeyVersion() => "patient-test-v1";
        public byte[]? GetHmacKeyByVersion(string? version) => version == "patient-test-v1" ? key : null;
        public byte[]? GetEncryptionKey() => null;
        public string GetEncryptionKeyVersion() => "";
        public byte[]? GetEncryptionKeyByVersion(string? version) => null;
    }

    public PacienteAtomicityTests()
    {
        Directory.CreateDirectory(root);
        using (var schema = new SQLite.SQLiteConnection(DbPath)) schema.CreateTable<Paciente>();
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

    private static Paciente NewPatient() => new()
    {
        NombreCompleto = "Paciente sintético", DNI = "DNI-sintetico", Telefono = "000", Email = "test@example.invalid",
        FechaNacimiento = new DateTime(1990, 2, 3), Sexo = "Prueba", Calle = "Calle sintética", Numero = "7",
        Piso = "2B", CP = "00000", Municipio = "Municipio", Provincia = "Provincia", ProteccionDatos = "Prueba",
        Firma = "firma-sintetica", FechaAlta = new DateTime(2026, 9, 30), FechaCreacion = new DateTime(2000, 1, 1)
    };

    private Paciente ReadPatient(int id)
    {
        using var connection = new SQLite.SQLiteConnection(DbPath);
        return connection.Find<Paciente>(id);
    }

    private static void AssertEquivalent(Paciente expected, Paciente actual)
    {
        // sqlite-net preserves DateTime ticks, not DateTime.Kind. Compare values,
        // rather than JSON's timezone suffix, for every persisted model field.
        foreach (var property in typeof(Paciente).GetProperties())
            Assert.Equal(property.GetValue(expected), property.GetValue(actual));
    }

    [Fact]
    public void SuccessfulOperationsPreserveAllFieldsAndCreateOneAuditEach()
    {
        var patient = NewPatient();
        var start = DateTime.Now;
        PacienteService.Guardar(patient, audit, DbPath);
        Assert.True(patient.Id > 0);
        Assert.InRange(patient.FechaCreacion, start, DateTime.Now);
        // Round trip through the existing sqlite-net reader catches missing fields and date format changes.
        AssertEquivalent(patient, ReadPatient(patient.Id));
        var createdAt = patient.FechaCreacion;
        patient.NombreCompleto = "Editado";
        patient.FechaNacimiento = null;
        patient.FechaAlta = null;
        patient.Firma = "firma-editada";
        PacienteService.Guardar(patient, audit, DbPath);
        Assert.Equal(createdAt, patient.FechaCreacion);
        AssertEquivalent(patient, ReadPatient(patient.Id));
        PacienteService.Eliminar(patient.Id, audit, DbPath);
        Assert.Equal(0L, Sql("SELECT COUNT(*) FROM Paciente"));
        Assert.Equal(3L, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Empty(audit.VerifyIntegrity());
        Assert.Equal("Paciente.Crear|Paciente.Editar|Paciente.Eliminar", Sql("SELECT group_concat(Accion,'|') FROM (SELECT Accion FROM Auditoria ORDER BY Id)"));
        foreach (var item in audit.GetRecentAudits(10))
        {
            using var payload = JsonDocument.Parse(item.Detalles!);
            Assert.Equal(patient.Id.ToString(), payload.RootElement.GetProperty("PacienteId").GetString());
        }
    }

    [Theory]
    [InlineData("create", "audit-insert")]
    [InlineData("update", "audit-insert")]
    [InlineData("delete", "audit-insert")]
    [InlineData("create", "key")]
    [InlineData("update", "key")]
    [InlineData("delete", "key")]
    [InlineData("create", "commit")]
    [InlineData("update", "commit")]
    [InlineData("delete", "commit")]
    public void FailureRollsBackBusinessAndAudit(string operation, string failure)
    {
        var patient = NewPatient();
        if (operation != "create") PacienteService.Guardar(patient, audit, DbPath);
        var originalJson = operation == "create" ? null : JsonSerializer.Serialize(ReadPatient(patient.Id));
        var originalId = patient.Id;
        var createdAt = patient.FechaCreacion;
        var auditCount = Sql("SELECT COUNT(*) FROM Auditoria");
        if (failure == "key") keys.Available = false;
        else if (failure == "audit-insert")
            Sql("CREATE TRIGGER reject_audit BEFORE INSERT ON Auditoria BEGIN SELECT RAISE(ABORT,'synthetic audit failure'); END;");
        else
            Sql("CREATE TABLE Parent (Id INTEGER PRIMARY KEY); CREATE TABLE DeferredFailure (Id INTEGER REFERENCES Parent(Id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER reject_commit AFTER INSERT ON Auditoria BEGIN INSERT INTO DeferredFailure VALUES (123); END;");
        patient.NombreCompleto = "Must not persist";
        patient.FechaNacimiento = null;
        patient.Firma = "changed";
        var error = Record.Exception(() =>
        {
            if (operation == "delete") PacienteService.Eliminar(patient.Id, audit, DbPath);
            else PacienteService.Guardar(patient, audit, DbPath);
        });
        if (failure == "key") Assert.IsType<InvalidOperationException>(error);
        else Assert.IsType<SqliteException>(error);
        Assert.Equal(auditCount, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Equal(operation == "create" ? 0L : 1L, Sql("SELECT COUNT(*) FROM Paciente"));
        Assert.Equal(originalId, patient.Id);
        Assert.Equal(createdAt, patient.FechaCreacion);
        if (operation != "create") Assert.Equal(originalJson, JsonSerializer.Serialize(ReadPatient(originalId)));
        if (failure == "commit") Assert.Equal(0L, Sql("SELECT COUNT(*) FROM DeferredFailure"));
    }

    [Fact]
    public void BusinessFailureDoesNotCreateSuccessAudit()
    {
        Sql("CREATE TRIGGER reject_business BEFORE INSERT ON Paciente BEGIN SELECT RAISE(ABORT,'synthetic business failure'); END;");
        Assert.Throws<SqliteException>(() => PacienteService.Guardar(NewPatient(), audit, DbPath));
        Assert.Equal(0L, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Equal(0L, Sql("SELECT COUNT(*) FROM Paciente"));
    }

    [Fact]
    public void MissingAuditOrDifferentDatabaseRejectsChanges()
    {
        var patient = NewPatient();
        PacienteService.Guardar(patient, audit, DbPath);
        Assert.Throws<InvalidOperationException>(() => PacienteService.Guardar(NewPatient(), null, DbPath));
        Assert.Throws<InvalidOperationException>(() => PacienteService.Eliminar(patient.Id, null, DbPath));
        var other = new AuditoriaService(new SqliteConnectionStringBuilder
            { DataSource = Path.Combine(root, "other.db"), Pooling = false }.ToString(), keys);
        Assert.Throws<InvalidOperationException>(() => PacienteService.Guardar(NewPatient(), other, DbPath));
        Assert.Throws<InvalidOperationException>(() => PacienteService.Eliminar(patient.Id, other, DbPath));
        Assert.Equal(1L, Sql("SELECT COUNT(*) FROM Paciente"));
        Assert.Equal(1L, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Empty(other.GetRecentAudits(10));
    }
}
