using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
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

        [Fact]
        public async Task WebhookFallido_PropagaElError_ParaQueLaColaLoReintente()
        {
            var previousUrl = Environment.GetEnvironmentVariable("AUDIT_WEBHOOK_URL");
            try
            {
                Environment.SetEnvironmentVariable("AUDIT_WEBHOOK_URL", "http://127.0.0.1:1/audit");
                var forwarder = new WebhookForwarder();

                await Assert.ThrowsAsync<InvalidOperationException>(() => forwarder.ForwardEventAsync("{}", "signature"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("AUDIT_WEBHOOK_URL", previousUrl);
            }
        }

        [Fact]
        public void BlobExistente_SoloSeAcepta_SiEventIdYFirmaCoinciden()
        {
            var metadata = new Dictionary<string, string>
            {
                ["eventId"] = "event-123",
                ["signature"] = "signature-123"
            };

            Assert.True(BlobAuditExporter.MetadataMatchesExistingEvent(metadata, "event-123", "signature-123"));
            Assert.False(BlobAuditExporter.MetadataMatchesExistingEvent(metadata, "event-123", "other-signature"));
            Assert.False(BlobAuditExporter.MetadataMatchesExistingEvent(metadata, "other-event", "signature-123"));
        }

        [Fact]
        public void BlobDeEvento_UsaLaFechaFirmada_ParaMantenerElMismoDestinoEnReintentos()
        {
            var payload = "{\"FechaHora\":\"2026-10-07T23:59:59.0000000Z\"}";

            Assert.Equal("20261007/event-123.json", BlobAuditExporter.BuildBlobName("event-123", payload));
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
