using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class AuditoriaIntegrityWorkerTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly string _reportDir;

        public AuditoriaIntegrityWorkerTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"auditoria_worker_{Guid.NewGuid():N}.db");
            _connectionString = $"Data Source={_dbPath}";

            // Ensure predictable keys for HMAC/encryption
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", "worker-test-key-0123456789");
            Environment.SetEnvironmentVariable("AUDIT_ENC_KEY", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("encryptionkey1234567890123456")));

            _reportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClinicaLongevidadApp", "AuditIntegrityReports");
            try { if (Directory.Exists(_reportDir)) Directory.Delete(_reportDir, true); } catch { }
        }

        [Fact]
        public async Task Worker_RaisesEvent_OnTamperDetected_And_WritesReport()
        {
            var service = new AuditoriaService(_connectionString);

            var evento1 = new AuditoriaEvento { UsuarioAdmin = "u1", Accion = "A1", Modulo = "M", Detalles = "{\"a\":1}" };
            var evento2 = new AuditoriaEvento { UsuarioAdmin = "u2", Accion = "A2", Modulo = "M", Detalles = "{\"b\":2}" };

            service.RegistrarEvento(evento1);
            service.RegistrarEvento(evento2);

            // Tamper with second row detalles
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Auditoria SET Detalles = 'tampered' WHERE Id = (SELECT MAX(Id) FROM Auditoria)";
                cmd.ExecuteNonQuery();
            }

            var worker = new AuditoriaIntegrityWorker(service, TimeSpan.FromSeconds(1));
            try
            {
                bool eventFired = false;
                string[]? firedErrors = null;
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                worker.OnIntegrityFailure += (errors) =>
                {
                    eventFired = true;
                    firedErrors = errors?.ToArray();
                    tcs.TrySetResult(true);
                };

                // Run once synchronously
                var runTask = worker.RunOnceAsync();

                // Wait for either the run to complete or the event to fire
                var completed = await Task.WhenAny(runTask, tcs.Task).ConfigureAwait(false);
                // Ensure runTask completed
                await runTask.ConfigureAwait(false);

                Assert.True(eventFired, "Expected OnIntegrityFailure to be fired when tamper present");
                Assert.NotNull(firedErrors);
                Assert.NotEmpty(firedErrors);

                // Check report files exist
                Assert.True(Directory.Exists(_reportDir), "Report directory should exist");
                var files = Directory.GetFiles(_reportDir).Where(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToArray();
                Assert.True(files.Length > 0, "Expected at least one report file to be written");
            }
            finally
            {
                try { worker.Dispose(); } catch { }
            }
        }

        [Fact]
        public async Task Worker_DoesNotRaiseEvent_WhenIntegrityOk()
        {
            var service = new AuditoriaService(_connectionString);

            var evento1 = new AuditoriaEvento { UsuarioAdmin = "u1", Accion = "A1", Modulo = "M", Detalles = "{\"a\":1}" };
            service.RegistrarEvento(evento1);

            var worker = new AuditoriaIntegrityWorker(service, TimeSpan.FromSeconds(1));
            try
            {
                bool eventFired = false;
                worker.OnIntegrityFailure += (errors) => eventFired = true;

                await worker.RunOnceAsync().ConfigureAwait(false);

                Assert.False(eventFired, "OnIntegrityFailure should not be fired when integrity is OK");
            }
            finally
            {
                try { worker.Dispose(); } catch { }
            }
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_reportDir)) Directory.Delete(_reportDir, true); } catch { }
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
