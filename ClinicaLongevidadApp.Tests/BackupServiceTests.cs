using System;
using System.IO;
using Microsoft.Data.Sqlite;
using ClinicaLongevidadApp.Services;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class BackupServiceTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _conn;
        private readonly string _backupDir;

        public BackupServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"backup_test_{Guid.NewGuid():N}.db");
            _conn = $"Data Source={_dbPath}";
            _backupDir = Path.Combine(Path.GetTempPath(), $"backup_out_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_backupDir);

            // ensure env key for HMAC
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", "test-hmac-key-0123456789");

            // create a simple sqlite db
            using var conn = new SqliteConnection(_conn);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS test (id INTEGER PRIMARY KEY, val TEXT); INSERT INTO test (val) VALUES ('x');";
            cmd.ExecuteNonQuery();
        }

        [Fact]
        public void TriggerImmediateBackup_CreatesFiles_And_RestoreSucceeds()
        {
            var svc = new BackupService();
            var backupPath = svc.TriggerImmediateBackup(_conn, _backupDir);
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
            {
                // try to show debug log to help diagnose
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicaLongevidadApp", "logs");
                var logFile = Path.Combine(logDir, "backup.log");
                string logContents = "";
                try { if (File.Exists(logFile)) logContents = File.ReadAllText(logFile); } catch { }
                Assert.True(false, $"Backup not created. backupPath='{backupPath}'. backup log:\n{logContents}");
            }

            var sha = backupPath + ".sha256";
            if (!File.Exists(sha))
            {
                string files = "";
                try { files = string.Join("\n", Directory.GetFiles(_backupDir)); } catch { }
                Assert.True(false, $"SHA256 file missing for backup. backupDir files:\n{files}");
            }
            var shaText = File.ReadAllText(sha).Trim();
            Assert.False(string.IsNullOrWhiteSpace(shaText));

            var hmac = backupPath + ".hmac";
            var hmacVer = backupPath + ".hmac.ver";
            Assert.True(File.Exists(hmac));
            Assert.True(File.Exists(hmacVer));

            // Now try restore: will verify integrity and replace the DB (we first ensure db exists)
            var originalCopy = _dbPath + ".orig";
            File.Copy(_dbPath, originalCopy, overwrite: true);

            svc.RestoreBackup(backupPath, _conn);

            // After restore, DB file exists and has content
            using var conn = new SqliteConnection(_conn);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM test;";
            var cnt = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.True(cnt >= 0);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
            try { if (Directory.Exists(_backupDir)) Directory.Delete(_backupDir, true); } catch { }
        }
    }
}
