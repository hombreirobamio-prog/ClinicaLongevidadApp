using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Text;

namespace ClinicaLongevidadApp.Services
{
    internal class AuditForwardQueue
    {
        private readonly string _connectionString;

        public AuditForwardQueue(string connectionString)
        {
            _connectionString = connectionString;
            EnsureTable();
        }

        private void EnsureTable()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS AuditForwardQueue (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                EventId TEXT NOT NULL,
                                Payload TEXT NOT NULL,
                                Signature TEXT,
                                Attempts INTEGER NOT NULL DEFAULT 0,
                                LastError TEXT,
                                NextAttemptAt TEXT NOT NULL,
                                CreatedAt TEXT NOT NULL
                            );";
            cmd.ExecuteNonQuery();
        }

        public void Enqueue(string eventId, string payload, string signature)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO AuditForwardQueue (EventId, Payload, Signature, Attempts, NextAttemptAt, CreatedAt)
                                    VALUES (@eid, @p, @s, 0, @next, @created);";
                cmd.Parameters.AddWithValue("@eid", eventId ?? string.Empty);
                cmd.Parameters.AddWithValue("@p", payload ?? string.Empty);
                cmd.Parameters.AddWithValue("@s", signature ?? string.Empty);
                cmd.Parameters.AddWithValue("@next", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // best-effort enqueue; avoid throwing from audit path
                try { LogService.Error("AuditForwardQueue", "Failed to enqueue audit forward", ex); } catch { }
            }
        }

        public List<QueueRow> GetPending(int maxRows)
        {
            var list = new List<QueueRow>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT Id, EventId, Payload, Signature, Attempts FROM AuditForwardQueue
                                WHERE datetime(NextAttemptAt) <= datetime('now')
                                ORDER BY Id LIMIT @max";
            cmd.Parameters.AddWithValue("@max", maxRows);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new QueueRow
                {
                    Id = reader.GetInt32(0),
                    EventId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Payload = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Signature = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Attempts = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                });
            }
            return list;
        }

        public void Delete(int id)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM AuditForwardQueue WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void MarkFailed(int id, string lastError, int attempts)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            // exponential backoff: base 30s
            int backoffSeconds = 30 * attempts;
            var next = DateTime.UtcNow.AddSeconds(backoffSeconds).ToString("o");
            cmd.CommandText = "UPDATE AuditForwardQueue SET Attempts = @a, LastError = @e, NextAttemptAt = @n WHERE Id = @id";
            cmd.Parameters.AddWithValue("@a", attempts);
            cmd.Parameters.AddWithValue("@e", lastError ?? string.Empty);
            cmd.Parameters.AddWithValue("@n", next);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public class QueueRow
        {
            public int Id { get; set; }
            public string EventId { get; set; } = string.Empty;
            public string Payload { get; set; } = string.Empty;
            public string Signature { get; set; } = string.Empty;
            public int Attempts { get; set; }
        }
    }

    internal class AuditForwardQueueWorker : IDisposable
    {
        private readonly string _connectionString;
        private readonly IWebhookForwarder? _forwarder;
        private readonly IAuditExporter? _exporter;
        private readonly AuditForwardQueue _queue;
        private readonly System.Threading.Timer? _timer;
        private readonly int _intervalMs;
        private readonly int _maxAttempts = 5;

        public AuditForwardQueueWorker(string connectionString, IWebhookForwarder? forwarder, IAuditExporter? exporter, int intervalSeconds = 30)
        {
            _connectionString = connectionString;
            _forwarder = forwarder;
            _exporter = exporter;
            _queue = new AuditForwardQueue(connectionString);
            _intervalMs = Math.Max(1000, intervalSeconds * 1000);
            _timer = new System.Threading.Timer(async _ => await ProcessOnceAsync().ConfigureAwait(false), null, _intervalMs, _intervalMs);
        }

        public async Task ProcessOnceAsync()
        {
            try
            {
                var pending = _queue.GetPending(25);
                foreach (var row in pending)
                {
                    try
                    {
                        if (_forwarder != null)
                        {
                            await _forwarder.ForwardEventAsync(row.Payload, row.Signature ?? string.Empty).ConfigureAwait(false);
                        }

                        if (_exporter != null)
                        {
                            await _exporter.ExportEventAsync(row.EventId ?? Guid.NewGuid().ToString("N"), row.Payload, row.Signature ?? string.Empty).ConfigureAwait(false);
                        }

                        // success -> delete
                        _queue.Delete(row.Id);
                    }
                    catch (Exception ex)
                    {
                        int attempts = row.Attempts + 1;
                        _queue.MarkFailed(row.Id, ex.Message, attempts);
                        if (attempts >= _maxAttempts)
                        {
                            try { LogService.Error("AuditForwardQueueWorker", $"Row {row.Id} failed after {attempts} attempts: {ex.Message}"); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try { LogService.Error("AuditForwardQueueWorker", "Processing failed", ex); } catch { }
            }
        }

        public void Dispose()
        {
            try { _timer?.Dispose(); } catch { }
        }
    }
}
