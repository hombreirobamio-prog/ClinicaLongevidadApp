using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ClinicaLongevidadApp.Tests;

// Every path and key belongs to this synthetic drill. No operational DB is accepted.
public class RecoveryDrillTests
{
    private sealed class DrillKeys : IKeyProvider
    {
        private readonly byte[] signing = RandomNumberGenerator.GetBytes(32);
        private readonly byte[] encryption = RandomNumberGenerator.GetBytes(32);
        public byte[] GetHmacKey() => signing;
        public string GetHmacKeyVersion() => "drill-v1";
        public byte[]? GetHmacKeyByVersion(string? version) => version == "drill-v1" ? signing : null;
        public byte[] GetEncryptionKey() => encryption;
        public string GetEncryptionKeyVersion() => "drill-enc-v1";
        public byte[]? GetEncryptionKeyByVersion(string? version) => version == "drill-enc-v1" ? encryption : null;
    }

    private static string Connection(string path) => new SqliteConnectionStringBuilder
    { DataSource = path, Pooling = false }.ToString();

    private static object? Sql(string path, string query)
    {
        using var connection = new SqliteConnection(Connection(path));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = query;
        return command.ExecuteScalar();
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed class InterruptedRestore : BackupService
    {
        public InterruptedRestore(IKeyProvider keys) : base(keys) { }
        protected override void ReplaceDatabase(string staged, string destination, string previous)
            => throw new IOException("Injected interruption immediately before atomic replacement.");
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("modified-db")]
    [InlineData("modified-db-and-sha")]
    [InlineData("forged-hmac")]
    [InlineData("unknown-version")]
    [InlineData("missing-sha")]
    [InlineData("missing-hmac")]
    [InlineData("missing-version")]
    [InlineData("active-file")]
    [InlineData("active-sqlite")]
    [InlineData("wal-sidecar")]
    [InlineData("interrupted-replace")]
    [InlineData("signed-invalid-sqlite")]
    public void IsolatedRecoveryRestoresExactSnapshotOrPreservesTarget(string scenario)
    {
        var runId = Guid.NewGuid().ToString("N");
        var baseDirectory = Environment.GetEnvironmentVariable("AUDIT_RECOVERY_EVIDENCE_DIR")
            ?? Path.Combine(Path.GetTempPath(), "audit-recovery-drills");
        var root = Path.Combine(Path.GetFullPath(baseDirectory), runId);
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "synthetic-source.db");
        var target = Path.Combine(root, "synthetic-target.db");
        var started = DateTime.UtcNow;
        var watch = Stopwatch.StartNew();
        var passed = false;
        string? backup = null;
        string? before = null;
        string? after = null;
        string? rejectedReason = null;
        long restoreMilliseconds = 0;
        try
        {
            var keys = new DrillKeys();
            Sql(source, "CREATE TABLE DrillData (Id INTEGER PRIMARY KEY, Value TEXT NOT NULL); INSERT INTO DrillData VALUES (1,'alpha'),(2,'beta'),(3,'gamma');");
            var audit = new AuditoriaService(Connection(source), keys);
            Assert.True(audit.IsInitialized);
            for (var i = 1; i <= 3; i++)
                audit.RegistrarEvento(new AuditoriaEvento { Accion = "Drill.Create", UsuarioAdmin = "synthetic-operator", Modulo = "RecoveryDrill", Detalles = $"synthetic-{i}" });
            Assert.Empty(audit.VerifyIntegrity());
            using var service = scenario == "interrupted-replace" ? new InterruptedRestore(keys) : new BackupService(keys);
            backup = service.TriggerImmediateBackup(Connection(source), Path.Combine(root, "backups"));
            Assert.NotNull(backup);
            Assert.True(File.Exists(backup));
            foreach (var suffix in new[] { ".sha256", ".hmac", ".hmac.ver" }) Assert.True(File.Exists(backup + suffix));

            // A later source change must not appear in the restored point-in-time copy.
            Sql(source, "INSERT INTO DrillData VALUES (4,'after-backup');");
            Sql(target, "CREATE TABLE DestinationMarker (Value TEXT); INSERT INTO DestinationMarker VALUES ('preserve-on-rejection');");
            before = Hash(target);
            switch (scenario)
            {
                case "modified-db":
                case "modified-db-and-sha":
                    Sql(backup!, "UPDATE DrillData SET Value='tampered' WHERE Id=1");
                    if (scenario == "modified-db-and-sha") File.WriteAllText(backup + ".sha256", Hash(backup!));
                    break;
                case "forged-hmac": File.WriteAllText(backup + ".hmac", new string('0', 64)); break;
                case "unknown-version": File.WriteAllText(backup + ".hmac.ver", "unknown"); break;
                case "missing-sha": File.Delete(backup + ".sha256"); break;
                case "missing-hmac": File.Delete(backup + ".hmac"); break;
                case "missing-version": File.Delete(backup + ".hmac.ver"); break;
                case "wal-sidecar": File.WriteAllText(target + "-wal", "synthetic-sidecar"); break;
                case "signed-invalid-sqlite":
                    File.WriteAllText(backup!, "authenticated bytes that are not a SQLite database");
                    File.WriteAllText(backup + ".sha256", Hash(backup!));
                    File.WriteAllText(backup + ".hmac", Convert.ToHexString(HMACSHA256.HashData(keys.GetHmacKey(), File.ReadAllBytes(backup!))));
                    break;
            }
            using var fileInUse = scenario == "active-file" ? new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite) : null;
            using var sqliteInUse = scenario == "active-sqlite" ? new SqliteConnection(Connection(target)) : null;
            sqliteInUse?.Open();
            using var activeTransaction = sqliteInUse?.BeginTransaction();
            var restoreWatch = Stopwatch.StartNew();
            if (scenario == "valid")
            {
                service.RestoreBackup(backup!, Connection(target));
                restoreWatch.Stop();
                Assert.Equal("ok", Sql(target, "PRAGMA integrity_check"));
                Assert.Equal("1:alpha|2:beta|3:gamma", Sql(target, "SELECT group_concat(item,'|') FROM (SELECT Id || ':' || Value AS item FROM DrillData ORDER BY Id)"));
                Assert.Equal(3L, Sql(target, "SELECT COUNT(*) FROM Auditoria"));
                var verifierConnection = new SqliteConnectionStringBuilder(Connection(target)) { Mode = SqliteOpenMode.ReadOnly };
                var verifier = new AuditoriaService(verifierConnection.ToString(), keys, initializeSchema: false);
                Assert.True(verifier.IsInitialized);
                Assert.Empty(verifier.VerifyIntegrity());
                var previous = Assert.Single(Directory.GetFiles(root, "synthetic-target_pre_restore_*.db"));
                Assert.Equal(before, Hash(previous));
                Assert.Equal(Hash(backup!), Hash(target));
            }
            else
            {
                var error = Record.Exception(() => service.RestoreBackup(backup!, Connection(target)));
                Assert.NotNull(error);
                if (scenario == "active-file" || scenario == "active-sqlite" || scenario == "interrupted-replace") Assert.IsType<IOException>(error);
                else if (scenario == "signed-invalid-sqlite") Assert.IsType<SqliteException>(error);
                else Assert.IsType<InvalidOperationException>(error);
                restoreWatch.Stop();
                rejectedReason = error.Message;
                activeTransaction?.Dispose();
                sqliteInUse?.Dispose();
                fileInUse?.Dispose();
                Assert.Equal(before, Hash(target));
                // The synthetic sidecar is intentionally not a SQLite WAL; don't open it.
                if (scenario != "wal-sidecar") Assert.Equal("preserve-on-rejection", Sql(target, "SELECT Value FROM DestinationMarker"));
                Assert.Empty(Directory.GetFiles(root, "synthetic-target_pre_restore_*.db"));
            }
            Assert.Empty(Directory.GetFiles(root, ".restore_*"));
            restoreMilliseconds = restoreWatch.ElapsedMilliseconds;
            after = Hash(target);
            passed = true;
        }
        finally
        {
            watch.Stop();
            var evidence = new
            {
                Scope = "synthetic-recovery-drill", RunId = runId, Scenario = scenario,
                StartedUtc = started, FinishedUtc = DateTime.UtcNow, Passed = passed,
                DurationMilliseconds = watch.ElapsedMilliseconds, RestoreOrRejectMilliseconds = restoreMilliseconds,
                ExpectedOutcome = scenario == "valid" ? "exact snapshot restored" : "rejected without target modification",
                TargetBeforeSHA256 = before, TargetAfterSHA256 = after,
                Backup = backup == null ? null : Path.GetRelativePath(root, backup), RejectedReason = rejectedReason,
                Limitation = "Synthetic small database; not production RTO/RPO. Ephemeral keys are not exported."
            };
            File.WriteAllText(Path.Combine(root, "recovery-result.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
