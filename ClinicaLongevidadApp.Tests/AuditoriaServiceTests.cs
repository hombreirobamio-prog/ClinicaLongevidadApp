using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class AuditoriaServiceTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public AuditoriaServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"auditoria_test_{Guid.NewGuid():N}.db");
            _connectionString = $"Data Source={_dbPath}";
            // ensure env key for HMAC
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", "test-key-1234567890");
        }

        [Fact]
        public void RegistrarEvento_Stores_Hash_And_Signature()
        {
            var service = new AuditoriaService(_connectionString);

            var evento = new AuditoriaEvento
            {
                UsuarioAdmin = "tester",
                Accion = "Test.Action",
                Modulo = "Tests",
                UsuarioAfectado = "paciente",
                Resultado = true,
                Detalles = "{\"foo\":\"bar\"}",
                Tipo = "Test"
            };

            service.RegistrarEvento(evento);

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT EventId, PrevHash, Hash, Signature, Detalles FROM Auditoria ORDER BY Id DESC LIMIT 1";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());

            string eventId = reader.GetString(0);
            string prevHash = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            string hash = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            string signature = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            string detalles = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);

            Assert.False(string.IsNullOrWhiteSpace(eventId));
            Assert.False(string.IsNullOrWhiteSpace(hash));
            Assert.False(string.IsNullOrWhiteSpace(signature));
            Assert.Contains("foo", detalles);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
