using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class AuditoriaConcurrencyTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public AuditoriaConcurrencyTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"auditoria_conc_{Guid.NewGuid():N}.db");
            _connectionString = $"Data Source={_dbPath}";

            // predictable keys for HMAC/encryption in tests
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", "concurrency-test-hmac-key-0123456789");
            Environment.SetEnvironmentVariable("AUDIT_ENC_KEY", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("encryptionkey1234567890123456")));
        }

        [Fact]
        public async Task RegistrarEvento_MultipleConcurrentWriters_MaintainsHashChain()
        {
            var service = new AuditoriaService(_connectionString);

            int writers = 25;
            int eventsPerWriter = 20;

            var tasks = new Task[writers];
            for (int w = 0; w < writers; w++)
            {
                int wi = w;
                tasks[w] = Task.Run(() =>
                {
                    for (int i = 0; i < eventsPerWriter; i++)
                    {
                        var ev = new AuditoriaEvento
                        {
                            UsuarioAdmin = $"user{wi}",
                            Accion = "concurrent-write",
                            Modulo = "Test",
                            Detalles = $"{{\"writer\":{wi},\"i\":{i}}}",
                            Resultado = true
                        };

                        // Use synchronous API as in production
                        service.RegistrarEvento(ev);
                    }
                });
            }

            await Task.WhenAll(tasks);

            // After all writes, VerifyIntegrity should report no errors
            var errors = service.VerifyIntegrity();
            Assert.NotNull(errors);
            Assert.Empty(errors);

            // Also validate row count
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM Auditoria;";
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.Equal(writers * eventsPerWriter, count);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
