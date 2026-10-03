using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ClinicaLongevidadApp.Tests;

public sealed class AuditAdminAtomicityTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "queue-admin-" + Guid.NewGuid().ToString("N") + ".db");
    private readonly Keys keys = new();
    private readonly AuditoriaService audit;
    private readonly AuditAdminService admin;
    private readonly string? previousRole = Sesion.RolActual;
    private readonly string? previousAuth = Environment.GetEnvironmentVariable("AUDIT_ENFORCE_AUTH");
    private string ConnectionString => new SqliteConnectionStringBuilder
        { DataSource = path, Pooling = false, ForeignKeys = true }.ToString();

    private sealed class Keys : IKeyProvider
    {
        public bool Available = true;
        private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
        public byte[] GetHmacKey() => Available ? key : Array.Empty<byte>();
        public string GetHmacKeyVersion() => "queue-admin-test-v1";
        public byte[]? GetHmacKeyByVersion(string? version) => version == GetHmacKeyVersion() ? key : null;
        public byte[]? GetEncryptionKey() => null;
        public string GetEncryptionKeyVersion() => "";
        public byte[]? GetEncryptionKeyByVersion(string? version) => null;
    }

    public AuditAdminAtomicityTests()
    {
        audit = new AuditoriaService(ConnectionString, keys);
        Assert.True(audit.IsInitialized);
        _ = new AuditForwardQueue(ConnectionString);
        admin = new AuditAdminService(ConnectionString);
        Sesion.RolActual = "Administración";
        Environment.SetEnvironmentVariable("AUDIT_ENFORCE_AUTH", "1");
        Sql(@"INSERT INTO AuditForwardDeadLetter (Id, EventId, Payload, Signature, Attempts, LastError, CreatedAt, FailedAt)
            VALUES (7, 'original-event', 'synthetic payload', 'synthetic signature', 5, 'synthetic error', '2026-09-29T10:00:00Z', '2026-09-30T10:00:00Z');");
    }

    public void Dispose()
    {
        Sesion.RolActual = previousRole;
        Environment.SetEnvironmentVariable("AUDIT_ENFORCE_AUTH", previousAuth);
    }

    private object? Sql(string sql)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private bool Change(bool requeue, int id = 7) =>
        requeue ? admin.RequeueDeadLetter(id, audit) : admin.DeleteDeadLetter(id, audit);

    private string Snapshot() => JsonSerializer.Serialize(admin.GetDeadLetter());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessPreservesQueueContentAndAddsExactlyOneAudit(bool requeue)
    {
        var start = DateTime.UtcNow;
        Assert.True(Change(requeue));
        Assert.Empty(admin.GetDeadLetter());
        if (requeue)
        {
            var row = Assert.Single(admin.GetPending());
            Assert.Equal("original-event", row.EventId);
            Assert.Equal("synthetic payload", row.Payload);
            Assert.Equal("synthetic signature", row.Signature);
            Assert.Equal("2026-09-29T10:00:00Z", row.CreatedAt);
            Assert.Equal(0, row.Attempts);
            Assert.Equal("", row.LastError);
            Assert.InRange(DateTime.Parse(row.NextAttemptAt).ToUniversalTime(), start, DateTime.UtcNow);
        }
        else Assert.Empty(admin.GetPending());
        var item = Assert.Single(audit.GetRecentAudits(10));
        Assert.Equal(requeue ? "AuditQueue.Reencolar" : "AuditQueue.Eliminar", item.Accion);
        Assert.Equal("original-event", item.UsuarioAfectado);
        using var details = JsonDocument.Parse(item.Detalles);
        Assert.Equal("7", details.RootElement.GetProperty("DeadLetterId").GetString());
        Assert.Equal("original-event", details.RootElement.GetProperty("EventIdAfectado").GetString());
        Assert.DoesNotContain("synthetic payload", item.Detalles);
        Assert.DoesNotContain("synthetic signature", item.Detalles);
        Assert.Empty(audit.VerifyIntegrity());
        Assert.False(Change(requeue));
        Assert.Single(audit.GetRecentAudits(10));
    }

    [Theory]
    [InlineData(false, "audit")]
    [InlineData(true, "audit")]
    [InlineData(false, "key")]
    [InlineData(true, "key")]
    [InlineData(false, "commit")]
    [InlineData(true, "commit")]
    [InlineData(false, "delete")]
    [InlineData(true, "delete")]
    [InlineData(true, "insert")]
    public void FailureRollsBackBothQueuesAndAudit(bool requeue, string failure)
    {
        var snapshot = Snapshot();
        if (failure == "key") keys.Available = false;
        else if (failure == "audit")
            Sql("CREATE TRIGGER reject_audit BEFORE INSERT ON Auditoria BEGIN SELECT RAISE(ABORT,'synthetic audit failure'); END;");
        else if (failure == "delete")
            Sql("CREATE TRIGGER reject_delete BEFORE DELETE ON AuditForwardDeadLetter BEGIN SELECT RAISE(ABORT,'synthetic delete failure'); END;");
        else if (failure == "insert")
            Sql("CREATE TRIGGER reject_insert BEFORE INSERT ON AuditForwardQueue BEGIN SELECT RAISE(ABORT,'synthetic insert failure'); END;");
        else
            Sql("CREATE TABLE Parent (Id INTEGER PRIMARY KEY); CREATE TABLE DeferredFailure (Id INTEGER REFERENCES Parent(Id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER reject_commit AFTER INSERT ON Auditoria BEGIN INSERT INTO DeferredFailure VALUES (123); END;");
        Assert.False(Change(requeue));
        Assert.Equal(snapshot, Snapshot());
        Assert.Empty(admin.GetPending());
        Assert.Empty(audit.GetRecentAudits(10));
        if (failure == "commit") Assert.Equal(0L, Sql("SELECT COUNT(*) FROM DeferredFailure"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingAuditDifferentDatabaseMissingIdAndUnauthorizedRoleRejectChanges(bool requeue)
    {
        var snapshot = Snapshot();
        Assert.False(requeue ? admin.RequeueDeadLetter(7, null) : admin.DeleteDeadLetter(7, null));
        var other = new AuditoriaService(new SqliteConnectionStringBuilder
            { DataSource = path + ".other", Pooling = false }.ToString(), keys);
        Assert.False(requeue ? admin.RequeueDeadLetter(7, other) : admin.DeleteDeadLetter(7, other));
        Assert.False(Change(requeue, 999));
        Sesion.RolActual = "Recepcion";
        Assert.False(Change(requeue));
        Assert.Equal(snapshot, Snapshot());
        Assert.Empty(admin.GetPending());
        Assert.Empty(audit.GetRecentAudits(10));
        Assert.Empty(other.GetRecentAudits(10));
    }

    [Fact]
    public async Task ConcurrentInterventionsOnSameRowHaveOnlyOneWinner()
    {
        var results = await Task.WhenAll(
            Task.Run(() => admin.RequeueDeadLetter(7, audit)),
            Task.Run(() => admin.DeleteDeadLetter(7, audit)));
        Assert.Single(results, result => result);
        Assert.Empty(admin.GetDeadLetter());
        Assert.Equal(results[0] ? 1 : 0, admin.GetPending().Count);
        Assert.Single(audit.GetRecentAudits(10));
        Assert.Empty(audit.VerifyIntegrity());
    }
}
