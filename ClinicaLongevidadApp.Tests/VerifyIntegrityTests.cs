using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class VerifyIntegrityTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public VerifyIntegrityTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"auditoria_verify_{Guid.NewGuid():N}.db");
            _connectionString = $"Data Source={_dbPath}";
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", "test-hmac-key-verify-123");
        }

        [Fact]
        public void VerifyIntegrity_ReturnsEmpty_ForSingleInsertedEvent()
        {
            var svc = new AuditoriaService(_connectionString);
            Assert.True(svc.IsInitialized);

            var evento = new AuditoriaEvento
            {
                UsuarioAdmin = "tester",
                Accion = "Verify.Test",
                Modulo = "Tests",
                UsuarioAfectado = "paciente",
                Resultado = true,
                Detalles = "{\"check\":\"ok\"}",
                Tipo = "Test"
            };

            svc.RegistrarEvento(evento);

            var errors = svc.VerifyIntegrity();

            Assert.NotNull(errors);
            Assert.Empty(errors);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
