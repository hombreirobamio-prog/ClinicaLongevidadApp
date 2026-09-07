using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class AuditForwardQueueTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public AuditForwardQueueTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"audit_queue_test_{Guid.NewGuid():N}.db");
            _connectionString = $"Data Source={_dbPath}";
            // Ensure environment keys predictable
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", "testkey0123456789");
            Environment.SetEnvironmentVariable("AUDIT_ENC_KEY", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("encryptionkey1234567890123456")));
        }

        [Fact]
        public async Task RegistrarEvento_Enqueues_WhenForwarderAlwaysFails()
        {
            var failingForwarder = new ThrowingForwarder();
            var service = new AuditoriaService(_connectionString, null, null, failingForwarder);

            var ev = new AuditoriaEvento { UsuarioAdmin = "t", Accion = "X", Modulo = "T", Detalles = "{\"x\":1}" };
            service.RegistrarEvento(ev);

            // wait until queue has an entry (the enqueue happens asynchronously after retries)
            bool found = await WaitForQueueCountAsync(1, TimeSpan.FromSeconds(8));
            Assert.True(found, "Expected queue to contain 1 item after forwarder failures");
        }

        [Fact]
        public async Task Worker_Processes_Enqueued_Item()
        {
            // Step 1: enqueue by using a failing forwarder
            var failingForwarder = new ThrowingForwarder();
            var service = new AuditoriaService(_connectionString, null, null, failingForwarder);

            var ev = new AuditoriaEvento { UsuarioAdmin = "t", Accion = "X", Modulo = "T", Detalles = "{\"x\":2}" };
            service.RegistrarEvento(ev);

            bool queued = await WaitForQueueCountAsync(1, TimeSpan.FromSeconds(8));
            Assert.True(queued, "Expected an item to be enqueued");

            // Step 2: create a recording forwarder and run the worker's ProcessOnceAsync via reflection
            var recordingForwarder = new RecordingForwarder();

            var asm = typeof(AuditoriaService).Assembly;
            var workerType = asm.GetType("ClinicaLongevidadApp.Services.AuditForwardQueueWorker");
            Assert.NotNull(workerType);

            // Create instance: (string connectionString, IWebhookForwarder? forwarder, IAuditExporter? exporter, int intervalSeconds = 30)
            var worker = Activator.CreateInstance(workerType, _connectionString, (object)recordingForwarder, null, 1);
            Assert.NotNull(worker);

            // Invoke ProcessOnceAsync
            var proc = workerType.GetMethod("ProcessOnceAsync", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(proc);

            var task = (Task)proc.Invoke(worker, null)!;
            await task;

            // After processing, queue should be empty and forwarder should have received payload
            bool empty = await WaitForQueueCountAsync(0, TimeSpan.FromSeconds(5));
            Assert.True(empty, "Expected queue to be empty after worker processed the item");

            Assert.True(recordingForwarder.Called, "Expected recording forwarder to be called by worker");
            Assert.False(string.IsNullOrEmpty(recordingForwarder.LastPayload));

            // Dispose worker if IDisposable
            if (worker is IDisposable disp) disp.Dispose();
        }

        private async Task<bool> WaitForQueueCountAsync(int expected, TimeSpan timeout)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(1) FROM AuditForwardQueue;";
                    var val = cmd.ExecuteScalar();
                    int count = 0;
                    if (val != null && val != DBNull.Value) count = Convert.ToInt32(val);
                    if (count == expected) return true;
                }
                catch { }
                await Task.Delay(250);
            }
            return false;
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }

        [Fact]
        public async Task Enqueued_Item_Reaches_MaxAttempts()
        {
            var failingForwarder = new ThrowingForwarder();
            var service = new AuditoriaService(_connectionString, null, null, failingForwarder);

            var ev = new AuditoriaEvento { UsuarioAdmin = "t", Accion = "X", Modulo = "T", Detalles = "{\"x\":3}" };
            service.RegistrarEvento(ev);

            bool queued = await WaitForQueueCountAsync(1, TimeSpan.FromSeconds(8));
            Assert.True(queued, "Expected an item to be enqueued");

            var asm = typeof(AuditoriaService).Assembly;
            var workerType = asm.GetType("ClinicaLongevidadApp.Services.AuditForwardQueueWorker");
            Assert.NotNull(workerType);

            var worker = Activator.CreateInstance(workerType, _connectionString, (object)failingForwarder, null, 1);
            Assert.NotNull(worker);

            var proc = workerType.GetMethod("ProcessOnceAsync", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(proc);

            int attempts = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(20))
            {
                var task = (Task)proc.Invoke(worker, null)!;
                await task;

                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, Attempts FROM AuditForwardQueue LIMIT 1";
                    using var reader = cmd.ExecuteReader();
                    int rowId = -1;
                    if (reader.Read())
                    {
                        rowId = reader.IsDBNull(0) ? -1 : reader.GetInt32(0);
                        attempts = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    }

                    // If attempts not yet at max, force NextAttemptAt into the past so the worker can pick it again quickly
                    if (rowId != -1 && attempts < 5)
                    {
                        using var upd = conn.CreateCommand();
                        upd.CommandText = "UPDATE AuditForwardQueue SET NextAttemptAt = @n WHERE Id = @id";
                        upd.Parameters.AddWithValue("@n", DateTime.UtcNow.AddSeconds(-1).ToString("o"));
                        upd.Parameters.AddWithValue("@id", rowId);
                        upd.ExecuteNonQuery();
                    }
                }
                catch { }

                if (attempts >= 5) break;

                await Task.Delay(200);
            }

            Assert.True(attempts >= 5, $"Expected attempts >=5 but was {attempts}");

            using (var conn2 = new SqliteConnection(_connectionString))
            {
                conn2.Open();
                using var cmd2 = conn2.CreateCommand();
                cmd2.CommandText = "SELECT COUNT(1) FROM AuditForwardQueue";
                var cnt = Convert.ToInt32(cmd2.ExecuteScalar() ?? 0);
                Assert.Equal(1, cnt);
            }

            if (worker is IDisposable disp) disp.Dispose();
        }

        private class ThrowingForwarder : IWebhookForwarder
        {
            public Task ForwardEventAsync(string jsonPayload, string signature)
            {
                return Task.FromException(new InvalidOperationException("simulated forward failure"));
            }
        }

        private class RecordingForwarder : IWebhookForwarder
        {
            public bool Called { get; private set; }
            public string LastPayload { get; private set; } = string.Empty;
            public string LastSignature { get; private set; } = string.Empty;

            public Task ForwardEventAsync(string jsonPayload, string signature)
            {
                Called = true;
                LastPayload = jsonPayload ?? string.Empty;
                LastSignature = signature ?? string.Empty;
                return Task.CompletedTask;
            }
        }
    }
}
