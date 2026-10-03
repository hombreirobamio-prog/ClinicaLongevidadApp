using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    class FakeForwarder : IWebhookForwarder
    {
        public TaskCompletionSource<(string payload, string signature)> Tcs { get; } = new();
        public Task ForwardEventAsync(string jsonPayload, string signature)
        {
            Tcs.TrySetResult((jsonPayload, signature));
            return Task.CompletedTask;
        }
    }

    public class WebhookForwarderIntegrationTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _conn;

        public WebhookForwarderIntegrationTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"auditoria_webhook_test_{Guid.NewGuid():N}.db");
            _conn = $"Data Source={_dbPath}";
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", "test-key-1234567890");
        }

        [Fact]
        public async Task RegistrarEvento_Encola_Y_Worker_Envia_A_Webhook()
        {
            var fake = new FakeForwarder();
            var svc = new AuditoriaService(_conn, forwarder: fake);

            var evento = new AuditoriaEvento
            {
                Accion = "restore",
                Modulo = "RotateKeys",
                UsuarioAdmin = "integ-test",
                Resultado = true,
                Detalles = "{\"note\":\"integration\"}",
                Tipo = "Operación"
            };

            svc.RegistrarEvento(evento);

            using (var connection = new SqliteConnection(_conn))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(1) FROM AuditForwardQueue;";
                Assert.Equal(1L, command.ExecuteScalar());
            }

            using var worker = new AuditForwardQueueWorker(_conn, fake, null, 60);
            await worker.ProcessOnceAsync();

            var completed = await Task.WhenAny(fake.Tcs.Task, Task.Delay(2000));
            Assert.True(completed == fake.Tcs.Task, "Forwarder did not receive payload in time");

            var (payload, signature) = await fake.Tcs.Task;
            Assert.False(string.IsNullOrWhiteSpace(payload));
            Assert.False(string.IsNullOrWhiteSpace(signature));

            using var doc = JsonDocument.Parse(payload);
            Assert.Equal("restore", doc.RootElement.GetProperty("Accion").GetString());
            Assert.Equal("RotateKeys", doc.RootElement.GetProperty("Modulo").GetString());
            Assert.Equal("integ-test", doc.RootElement.GetProperty("UsuarioAdmin").GetString());
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
