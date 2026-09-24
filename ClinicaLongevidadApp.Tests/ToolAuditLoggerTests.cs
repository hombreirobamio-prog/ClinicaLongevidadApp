using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class ToolAuditLoggerTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public ToolAuditLoggerTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"auditoria_tools_logger_test_{Guid.NewGuid():N}.db");
            _connectionString = $"Data Source={_dbPath}";
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", "test-key-1234567890");
        }

        [Fact]
        public void RegistrarOperacion_Inserts_Audit_Row()
        {
            var detalles = new { action = "test", info = "data" };
            ToolAuditLogger.RegistrarOperacion(_connectionString, "test-op", true, detalles, usuario: "unit-tester");

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            // Prefer the explicit DetallesPlain column when available (tests should not fail if Detalles is stored encrypted).
            cmd.CommandText = "SELECT Accion, Resultado, UsuarioAdmin, COALESCE(DetallesPlain, Detalles) AS DetallesCombined FROM Auditoria ORDER BY Id DESC LIMIT 1";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());

            Assert.Equal("test-op", reader.GetString(0));
            Assert.Equal("OK", reader.GetString(1));
            Assert.Equal("unit-tester", reader.GetString(2));
            var detallesStored = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            Assert.Contains("info", detallesStored);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
