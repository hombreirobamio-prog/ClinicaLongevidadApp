using System;
using System.IO;
using System.Timers;
using System.Text;
using Microsoft.Data.Sqlite;

namespace ClinicaLongevidadApp.Services
{
    /// <summary>
    /// Simple backup service for the SQLite audit DB.
    /// Supports immediate backup and scheduling a daily backup at a given time.
    /// </summary>
    public class BackupService : IDisposable
    {
        private Timer? _timer;
        private readonly object _sync = new object();
        private DateTime? _nextRun;
        private readonly IKeyProvider? _keyProvider;
        // Event fired when a backup has been created. Parameter: full path to created backup file.
        public event System.Action<string>? BackupCompleted;

        // Internal debug log file to help trace scheduling during development/tests
        private void WriteDebugLog(string message)
        {
            try
            {
                var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicaLongevidadApp", "logs");
                Directory.CreateDirectory(baseDir);
                var file = Path.Combine(baseDir, "backup.log");
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n";
                File.AppendAllText(file, line, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // Avoid recursive logging on failure to write the debug log.
                // Swallow the error silently to avoid throwing from diagnostic logging.
                _ = ex;
            }
        }

        public BackupService()
            : this(null)
        {
        }

        public BackupService(IKeyProvider? keyProvider)
        {
            _keyProvider = keyProvider ?? new LocalKeyProvider();
        }

        /// <summary>
        /// Returns the next scheduled run (local time) if any.
        /// </summary>
        public DateTime? NextScheduledRun => _nextRun;

        /// <summary>
        /// Trigger an immediate backup using the same logic as scheduled jobs. Runs synchronously and returns created path or null on failure.
        /// </summary>
        public string? TriggerImmediateBackup(string connectionString, string? backupDir = null)
        {
            try
            {
                var created = CreateBackup(connectionString, backupDir);
                try { WriteDebugLog($"TriggerImmediateBackup created: {created}"); } catch { }
                try { BackupCompleted?.Invoke(created); } catch { }
                return created;
            }
            catch (Exception ex)
            {
                try { WriteDebugLog($"TriggerImmediateBackup failed: {ex.Message}\n{ex.StackTrace}"); } catch { }
                AuditLogHelper.Warning("BackupService", $"TriggerImmediateBackup failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a filesystem copy of the SQLite DB referenced by the connection string.
        /// If connection string is not file-based, throws InvalidOperationException.
        /// </summary>
        public string CreateBackup(string connectionString, string? backupDir = null)
        {
            var dbFile = GetSqliteFilePathFromConnectionString(connectionString);
            if (string.IsNullOrEmpty(dbFile) || !File.Exists(dbFile))
            {
                AuditLogHelper.Error("BackupService", $"Database file not found or connection string is not file-based. ConnectionString='{connectionString}'");
                throw new InvalidOperationException("Database file not found or connection string is not file-based.");
            }

            var dir = backupDir ?? Path.GetDirectoryName(dbFile) ?? Environment.CurrentDirectory;
            Directory.CreateDirectory(dir);

            var name = Path.GetFileNameWithoutExtension(dbFile);
            var ext = Path.GetExtension(dbFile);
            var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(dir, $"{name}_backup_{ts}{ext}");
            AuditLogHelper.Info("BackupService", $"Starting backup for DB '{dbFile}'");
            // Try online backup via SQLite API to avoid file-lock issues. Fall back to File.Copy if that fails.
            try
            {
                // Build robust connection strings using the SqliteConnectionStringBuilder so we can
                // request a read-only shared cache for the source and a ReadWriteCreate destination.
                var srcBuilder = new SqliteConnectionStringBuilder(connectionString);
                if (string.IsNullOrEmpty(srcBuilder.DataSource))
                    throw new InvalidOperationException("Database file not found or connection string is not file-based.");

                var dstBuilder = new SqliteConnectionStringBuilder
                {
                    DataSource = backupPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Shared
                };

                // Open connections and set a busy timeout; use the online backup API with retries.
                using (var src = new SqliteConnection(srcBuilder.ToString()))
                using (var dst = new SqliteConnection(dstBuilder.ToString()))
                {
                    AuditLogHelper.Info("BackupService", "Opening source and destination SQLite connections for online backup.");
                    src.Open();
                    dst.Open();

                    using (var c = src.CreateCommand())
                    {
                        c.CommandText = "PRAGMA busy_timeout = 3000;";
                        c.ExecuteNonQuery();
                    }
                    using (var c = dst.CreateCommand())
                    {
                        c.CommandText = "PRAGMA busy_timeout = 3000;";
                        c.ExecuteNonQuery();
                    }

                    // Use the BackupDatabase overload with progress and retry to avoid hangs on busy DBs.
                    // Named parameters are used to remain resilient to overload ordering.
                    // Use the simple BackupDatabase overload; busy timeout and shared cache reduce chance of hangs.
                    src.BackupDatabase(dst);
                    AuditLogHelper.Info("BackupService", $"Online backup completed successfully to '{backupPath}'");
                }

                // After creating backup, compute checksum and optional HMAC signature for integrity
                try
                {
                    ComputeAndWriteChecksums(backupPath);
                }
                catch (Exception ex)
                {
                    try { WriteDebugLog($"ComputeAndWriteChecksums failed: {ex.Message}\n{ex.StackTrace}"); } catch { }
                    AuditLogHelper.Warning("BackupService", $"ComputeAndWriteChecksums failed: {ex.Message}");
                    throw;
                }

                return backupPath;
            }
            catch (Exception ex)
            {
                AuditLogHelper.Warning("BackupService", $"Online backup failed: {ex.Message}. Falling back to file copy.");
                try
                {
                    File.Copy(dbFile, backupPath, overwrite: false);
                    AuditLogHelper.Info("BackupService", $"File copy backup completed successfully to '{backupPath}'");
                    // Ensure integrity artifacts are written even when falling back to file copy.
                    try
                    {
                        ComputeAndWriteChecksums(backupPath);
                    }
                    catch (Exception ex3)
                    {
                        try { WriteDebugLog($"ComputeAndWriteChecksums (fallback) failed: {ex3.Message}"); } catch { }
                        AuditLogHelper.Warning("BackupService", $"ComputeAndWriteChecksums (fallback) failed: {ex3.Message}");
                    }

                    return backupPath;
                }
                catch (Exception ex2)
                {
                    AuditLogHelper.Error("BackupService", $"File copy backup failed: {ex2.Message}", ex2);
                    throw;
                }
            }
        }

        /// <summary>
/// Schedule a daily backup at the specified local time. Returns the next run DateTime (local) if scheduled, or null on failure.
        /// </summary>
 public DateTime? ScheduleDailyBackup(TimeSpan localTime, string connectionString, string? backupDir = null)
        {
            lock (_sync)
            {
                _timer?.Dispose();

                var now = DateTime.Now;
                var next = new DateTime(now.Year, now.Month, now.Day, localTime.Hours, localTime.Minutes, 0);
                if (next <= now) next = next.AddDays(1);

                var msUntil = (next - now).TotalMilliseconds;
                AuditLogHelper.Info("BackupService", $"Scheduling backup. Next run at {next:yyyy-MM-dd HH:mm:ss} (in {msUntil} ms). backupDir='{backupDir}'");
                try { WriteDebugLog($"Scheduling backup. Next run at {next:yyyy-MM-dd HH:mm:ss} (in {msUntil} ms). backupDir='{backupDir}'"); } catch { }
                _timer = new Timer(msUntil);
                _timer.AutoReset = false;
                _timer.Elapsed += (s, e) => OnTimerElapsed(localTime, connectionString, backupDir);
                _timer.Start();
                // expose next scheduled run
                try { _nextRun = next; } catch { }
                return next;
            }
        }

        private void OnTimerElapsed(TimeSpan localTime, string connectionString, string? backupDir)
        {
            try
            {
                AuditLogHelper.Info("BackupService", $"OnTimerElapsed triggered at {DateTime.Now:yyyy-MM-dd HH:mm:ss}. Performing backup.");
                try { WriteDebugLog($"OnTimerElapsed triggered at {DateTime.Now:yyyy-MM-dd HH:mm:ss}. Performing backup."); } catch { }

                string? created = null;
                try
                {
                    created = CreateBackup(connectionString, backupDir);
                    if (!string.IsNullOrWhiteSpace(created))
                    {
                        try { WriteDebugLog($"Backup created: {created}"); } catch { }
                        try { BackupCompleted?.Invoke(created); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    AuditLogHelper.Warning("BackupService", $"OnTimerElapsed: backup failed: {ex.Message}");
                }

                // schedule next run in 24 hours
                lock (_sync)
                {
                    try { _timer?.Dispose(); } catch { }
                    _timer = new Timer(TimeSpan.FromDays(1).TotalMilliseconds);
                    _timer.AutoReset = true;
                    _timer.Elapsed += (s, e) =>
                    {
                        try
                        {
                            var created2 = CreateBackup(connectionString, backupDir);
                            if (!string.IsNullOrWhiteSpace(created2))
                            {
                                try { WriteDebugLog($"Recurring backup created: {created2}"); } catch { }
                                try { BackupCompleted?.Invoke(created2); } catch { }
                            }
                        }
                        catch (Exception ex) { AuditLogHelper.Warning("BackupService", $"Recurring backup failed: {ex.Message}"); }
                    };
                    _timer.Start();
                    AuditLogHelper.Info("BackupService", "Next recurring backup scheduled in 24 hours.");
                    try { WriteDebugLog("Next recurring backup scheduled in 24 hours."); } catch { }
                }
            }
            catch (Exception ex)
            {
                AuditLogHelper.Error("BackupService", $"OnTimerElapsed fatal: {ex.Message}", ex);
            }
        }

        public void CancelScheduledBackup()
        {
            lock (_sync)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }

        public void Dispose()
        {
            CancelScheduledBackup();
        }

        /// <summary>
        /// Restore the SQLite database from a backup file. The current DB will be preserved by creating a .pre_restore timestamp copy.
        /// </summary>
        public void RestoreBackup(string backupFilePath, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath)) throw new ArgumentNullException(nameof(backupFilePath));
            if (!File.Exists(backupFilePath)) throw new FileNotFoundException("Backup file not found.", backupFilePath);

            var dbFile = GetSqliteFilePathFromConnectionString(connectionString);
            if (string.IsNullOrEmpty(dbFile) || !File.Exists(dbFile))
            {
                AuditLogHelper.Error("BackupService", $"Cannot restore: target database file not found. ConnectionString='{connectionString}'");
                throw new InvalidOperationException("Database file not found or connection string is not file-based.");
            }

            var dir = Path.GetDirectoryName(dbFile) ?? Environment.CurrentDirectory;
            var name = Path.GetFileNameWithoutExtension(dbFile);
            var ext = Path.GetExtension(dbFile);
            var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var preRestore = Path.Combine(dir, $"{name}_pre_restore_{ts}{ext}");

            // Verify checksum/HMAC before restore if present
            try
            {
                VerifyBackupIntegrity(backupFilePath);
            }
            catch (Exception ex)
            {
                AuditLogHelper.Warning("BackupService", $"Backup integrity verification failed: {ex.Message}");
                throw;
            }

            // Preserve current DB
            File.Copy(dbFile, preRestore, overwrite: false);

            // Copy backup over current DB (overwrite)
            File.Copy(backupFilePath, dbFile, overwrite: true);

            AuditLogHelper.Info("BackupService", $"Database restored from '{backupFilePath}'. Previous DB saved as '{preRestore}'");
        }

        private void ComputeAndWriteChecksums(string backupPath)
        {
            // Compute SHA256. Open with ReadWrite share to allow reading the file while another handle
            // (e.g., SQLite/native) still has it open for a short time.
            using var fs = File.Open(backupPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(fs);
            var hex = Convert.ToHexString(hash).ToLowerInvariant();
            File.WriteAllText(backupPath + ".sha256", hex, Encoding.UTF8);

            // Compute HMAC if key available
            try
            {
                var hmacKey = _keyProvider?.GetHmacKey();
                if (hmacKey != null && hmacKey.Length > 0)
                {
                    fs.Seek(0, SeekOrigin.Begin);
                    using var h = new System.Security.Cryptography.HMACSHA256(hmacKey);
                    var mac = h.ComputeHash(fs);
                    var macHex = Convert.ToHexString(mac).ToLowerInvariant();
                    File.WriteAllText(backupPath + ".hmac", macHex, Encoding.UTF8);
                    var ver = _keyProvider?.GetHmacKeyVersion() ?? string.Empty;
                    File.WriteAllText(backupPath + ".hmac.ver", ver, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                try { WriteDebugLog($"ComputeAndWriteChecksums HMAC failed: {ex.Message}"); } catch { }
                AuditLogHelper.Warning("BackupService", $"ComputeAndWriteChecksums HMAC failed: {ex.Message}");
            }
        }

        private void VerifyBackupIntegrity(string backupPath)
        {
            // Verify SHA256 if present
            var shaPath = backupPath + ".sha256";
            if (File.Exists(shaPath))
            {
                var expected = File.ReadAllText(shaPath).Trim();
                using var fs = OpenFileWithRetry(backupPath);
                using var sha = System.Security.Cryptography.SHA256.Create();
                var actual = Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("SHA256 checksum mismatch for backup file.");
                }
            }

            // Verify HMAC if present
            var hmacPath = backupPath + ".hmac";
            var hmacVerPath = backupPath + ".hmac.ver";
            if (File.Exists(hmacPath))
            {
                var expectedMac = File.ReadAllText(hmacPath).Trim();
                var ver = File.Exists(hmacVerPath) ? File.ReadAllText(hmacVerPath).Trim() : null;
                var key = _keyProvider?.GetHmacKeyByVersion(ver);
                if (key == null || key.Length == 0) throw new InvalidOperationException("HMAC key for backup verification not available.");
                using var fs = OpenFileWithRetry(backupPath);
                using var h = new System.Security.Cryptography.HMACSHA256(key);
                var actual = Convert.ToHexString(h.ComputeHash(fs)).ToLowerInvariant();
                if (!string.Equals(expectedMac, actual, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("HMAC verification failed for backup file.");
                }
            }
        }

        private FileStream OpenFileWithRetry(string path, int attempts = 3, int delayMs = 200)
        {
            for (int i = 0; ; i++)
            {
                try
                {
                    // Open for read and allow other processes to read/write briefly while still creating the stream.
                    return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch (IOException) when (i < attempts - 1)
                {
                    // transient lock; wait a bit and retry
                    try { System.Threading.Thread.Sleep(delayMs); } catch { }
                    continue;
                }
            }
        }

        private static string? GetSqliteFilePathFromConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return null;
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                if (p.StartsWith("Data Source", StringComparison.OrdinalIgnoreCase) || p.StartsWith("DataSource", StringComparison.OrdinalIgnoreCase))
                {
                    var eq = p.IndexOf('=');
                    if (eq >= 0 && eq + 1 < p.Length)
                    {
                        var val = p[(eq + 1)..].Trim();
                        if ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'")))
                            val = val[1..^1];
                        if (string.Equals(val, ":memory:", StringComparison.OrdinalIgnoreCase)) return null;
                        return val;
                    }
                }
            }
            return null;
        }
    }
}
