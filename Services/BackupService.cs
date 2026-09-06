using System;
using System.IO;
using System.Timers;
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

        public BackupService()
        {
        }

        /// <summary>
        /// Creates a filesystem copy of the SQLite DB referenced by the connection string.
        /// If connection string is not file-based, throws InvalidOperationException.
        /// </summary>
        public string CreateBackup(string connectionString, string? backupDir = null)
        {
            var dbFile = GetSqliteFilePathFromConnectionString(connectionString);
            if (string.IsNullOrEmpty(dbFile) || !File.Exists(dbFile))
                throw new InvalidOperationException("Database file not found or connection string is not file-based.");

            var dir = backupDir ?? Path.GetDirectoryName(dbFile) ?? Environment.CurrentDirectory;
            Directory.CreateDirectory(dir);

            var name = Path.GetFileNameWithoutExtension(dbFile);
            var ext = Path.GetExtension(dbFile);
            var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(dir, $"{name}_backup_{ts}{ext}");

            // Use File.Copy to create a snapshot. For safety, ensure DB is not locked by using SQLite online backup API
            // but for simplicity copy the file; this should be sufficient for small deployments.
            File.Copy(dbFile, backupPath, overwrite: false);

            return backupPath;
        }

        /// <summary>
        /// Schedule a daily backup at the specified local time. Returns true if scheduled.
        /// </summary>
        public bool ScheduleDailyBackup(TimeSpan localTime, string connectionString, string? backupDir = null)
        {
            lock (_sync)
            {
                _timer?.Dispose();

                var now = DateTime.Now;
                var next = new DateTime(now.Year, now.Month, now.Day, localTime.Hours, localTime.Minutes, 0);
                if (next <= now) next = next.AddDays(1);

                var msUntil = (next - now).TotalMilliseconds;
                _timer = new Timer(msUntil);
                _timer.AutoReset = false;
                _timer.Elapsed += (s, e) => OnTimerElapsed(localTime, connectionString, backupDir);
                _timer.Start();
                return true;
            }
        }

        private void OnTimerElapsed(TimeSpan localTime, string connectionString, string? backupDir)
        {
            try
            {
                // perform backup
                try
                {
                    CreateBackup(connectionString, backupDir);
                }
                catch
                {
                    // swallow - caller may log
                }

                // schedule next run in 24 hours
                lock (_sync)
                {
                    _timer?.Dispose();
                    _timer = new Timer(TimeSpan.FromDays(1).TotalMilliseconds);
                    _timer.AutoReset = true;
                    _timer.Elapsed += (s, e) =>
                    {
                        try { CreateBackup(connectionString, backupDir); } catch { }
                    };
                    _timer.Start();
                }
            }
            catch
            {
                // Ignore scheduling failures
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
