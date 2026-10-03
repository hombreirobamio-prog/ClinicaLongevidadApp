using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using Xunit;

namespace ClinicaLongevidadApp.Tests;

public sealed class FestivoAtomicityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "festivo-atomic-" + Guid.NewGuid().ToString("N"));
    private readonly string? previousRole = Sesion.RolActual;
    private readonly Keys keys = new();
    private string PathToDb => Path.Combine(root, "synthetic.db");
    private string ConnectionString => new SqliteConnectionStringBuilder
        { DataSource = PathToDb, Pooling = false, ForeignKeys = true }.ToString();
    private readonly AuditoriaService audit;

    private sealed class Keys : IKeyProvider
    {
        public bool Available = true;
        private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
        public byte[] GetHmacKey() => Available ? key : Array.Empty<byte>();
        public string GetHmacKeyVersion() => "synthetic-v1";
        public byte[]? GetHmacKeyByVersion(string? version) => version == "synthetic-v1" ? key : null;
        public byte[]? GetEncryptionKey() => null;
        public string GetEncryptionKeyVersion() => "";
        public byte[]? GetEncryptionKeyByVersion(string? version) => null;
    }

    public FestivoAtomicityTests()
    {
        Directory.CreateDirectory(root);
        Sesion.RolActual = "Administración";
        using (var schema = new SQLite.SQLiteConnection(PathToDb)) schema.CreateTable<Festivo>();
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

    private static Festivo NewHoliday() => new()
        { Nombre = "Synthetic holiday", Fecha = new DateTime(2026, 10, 1, 12, 30, 0), Tipo = " Local ", Activo = true };

    [Fact]
    public void SuccessfulOperationsCommitBusinessAndValidAuditTogether()
    {
        var holiday = NewHoliday();
        FestivoService.Guardar(holiday, audit, PathToDb);
        Assert.True(holiday.Id > 0);
        using (var reader = new SQLite.SQLiteConnection(PathToDb))
        {
            var stored = reader.Find<Festivo>(holiday.Id);
            Assert.Equal(holiday.Fecha.Date, stored.Fecha);
            Assert.Equal("Local", stored.Tipo);
            Assert.True(stored.Activo);
        }
        holiday.Nombre = "Updated";
        holiday.Activo = false;
        FestivoService.Guardar(holiday, audit, PathToDb);
        Assert.Equal("Updated", Sql("SELECT Nombre FROM Festivo"));
        Assert.Equal(0L, Sql("SELECT Activo FROM Festivo"));
        FestivoService.Eliminar(holiday.Id, audit, PathToDb);
        Assert.Equal(0L, Sql("SELECT COUNT(*) FROM Festivo"));
        Assert.Equal(3L, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Empty(audit.VerifyIntegrity());
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
        var holiday = NewHoliday();
        if (operation != "create") FestivoService.Guardar(holiday, audit, PathToDb);
        var auditCount = Sql("SELECT COUNT(*) FROM Auditoria");
        if (failure == "key") keys.Available = false;
        else if (failure == "audit-insert")
            Sql("CREATE TRIGGER reject_audit BEFORE INSERT ON Auditoria BEGIN SELECT RAISE(ABORT,'synthetic audit failure'); END;");
        else
        {
            // Force COMMIT (not INSERT) to fail with a deferred foreign key.
            Sql("CREATE TABLE Parent (Id INTEGER PRIMARY KEY); CREATE TABLE DeferredFailure (Id INTEGER REFERENCES Parent(Id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER reject_commit AFTER INSERT ON Auditoria BEGIN INSERT INTO DeferredFailure VALUES (123); END;");
        }
        holiday.Nombre = "Must not persist";
        Assert.ThrowsAny<Exception>(() =>
        {
            if (operation == "delete") FestivoService.Eliminar(holiday.Id, audit, PathToDb);
            else FestivoService.Guardar(holiday, audit, PathToDb);
        });
        Assert.Equal(auditCount, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Equal(operation == "create" ? 0L : 1L, Sql("SELECT COUNT(*) FROM Festivo"));
        if (operation == "create") Assert.Equal(0, holiday.Id);
        else Assert.Equal("Synthetic holiday", Sql("SELECT Nombre FROM Festivo"));
        if (failure == "commit") Assert.Equal(0L, Sql("SELECT COUNT(*) FROM DeferredFailure"));
    }

    [Fact]
    public void BusinessFailureDoesNotCreateAudit()
    {
        Sql("CREATE TRIGGER reject_business BEFORE INSERT ON Festivo BEGIN SELECT RAISE(ABORT,'synthetic business failure'); END;");
        Assert.Throws<SqliteException>(() => FestivoService.Guardar(NewHoliday(), audit, PathToDb));
        Assert.Equal(0L, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Equal(0L, Sql("SELECT COUNT(*) FROM Festivo"));
    }

    [Fact]
    public void MissingAuditOrDifferentDatabaseCannotPersistBusinessChanges()
    {
        Assert.Throws<InvalidOperationException>(() => FestivoService.Guardar(NewHoliday(), null, PathToDb));
        Assert.Throws<InvalidOperationException>(() => FestivoService.Eliminar(1, null, PathToDb));
        var otherPath = Path.Combine(root, "other.db");
        var otherAudit = new AuditoriaService(new SqliteConnectionStringBuilder { DataSource = otherPath, Pooling = false }.ToString(), keys);
        Assert.Throws<InvalidOperationException>(() => FestivoService.Guardar(NewHoliday(), otherAudit, PathToDb));
        Assert.Equal(0L, Sql("SELECT COUNT(*) FROM Festivo"));
        Assert.Empty(otherAudit.GetRecentAudits(10));
    }

    public void Dispose()
    {
        Sesion.RolActual = previousRole;
        // Keep the uniquely named synthetic databases for diagnosis; no shared paths are deleted.
    }
}