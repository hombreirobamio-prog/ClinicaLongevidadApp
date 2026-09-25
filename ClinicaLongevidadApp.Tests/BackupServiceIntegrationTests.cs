using System;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using ClinicaLongevidadApp.Services;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class BackupServiceIntegrationTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _conn;
        private readonly string _backupDir;

        public BackupServiceIntegrationTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"backup_integ_{Guid.NewGuid():N}.db");
            _conn = $"Data Source={_dbPath}";
            _backupDir = Path.Combine(Path.GetTempPath(), $"backup_integ_out_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_backupDir);

            using var conn = new SqliteConnection(_conn);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS test (id INTEGER PRIMARY KEY, val TEXT); INSERT INTO test (val) VALUES ('x');";
            cmd.ExecuteNonQuery();
        }

        [Fact]
        public void CreateBackup_Succeeds_WhenSourceHasOpenTransaction()
        {
            var svc = new BackupService();

            // Open a connection and begin a transaction to make the DB busy.
            using var busyConn = new SqliteConnection(_conn);
            busyConn.Open();
            using var tx = busyConn.BeginTransaction();

            // Trigger backup while DB is busy; BackupService should use the online backup API and succeed.
            var backupPath = svc.TriggerImmediateBackup(_conn, _backupDir);
            Assert.False(string.IsNullOrWhiteSpace(backupPath));
            Assert.True(File.Exists(backupPath), "Backup file was not created.");

            // Allow a short window for checksum files to be generated
            var sha = backupPath + ".sha256";
            var attempts = 10;
            var shaExists = false;
            for (int i = 0; i < attempts; i++)
            {
                if (File.Exists(sha)) { shaExists = true; break; }
                Thread.Sleep(200);
            }
            Assert.True(shaExists, "SHA256 checksum file was not created for backup.");
        }

        [Fact]
        public void RestoreBackup_Throws_WhenShaMismatch()
        {
            var svc = new BackupService();

            // Create a backup file by copying the DB
            var backupPath = Path.Combine(_backupDir, "manual_backup.db");
            File.Copy(_dbPath, backupPath, overwrite: true);

            // Write an invalid SHA file to simulate tampering
            File.WriteAllText(backupPath + ".sha256", "deadbeef", System.Text.Encoding.UTF8);

            // Ensure target DB exists so RestoreBackup attempts verification
            using (var conn = new SqliteConnection(_conn)) { conn.Open(); }

            var ex = Assert.Throws<InvalidOperationException>(() => svc.RestoreBackup(backupPath, _conn));
            Assert.Contains("checksum", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
            try { if (Directory.Exists(_backupDir)) Directory.Delete(_backupDir, true); } catch { }
        }
    }
}
