using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class ToolsAuditLoggingTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public ToolsAuditLoggingTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"auditoria_tools_test_{Guid.NewGuid():N}.db");
            _connectionString = $"Data Source={_dbPath}";
            // ensure env key for HMAC
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", "test-key-1234567890");
        }

        [Fact]
        public void ExportDelta_Event_Is_Stored()
        {
            var svc = new AuditoriaService(_connectionString);

            var detalles = new { backup = "backup.db", outfile = "delta.json", rows = 5, sourceDb = "Data Source=auditoria.db" };

            var evento = new AuditoriaEvento
            {
                Accion = "export-delta",
                Modulo = "RotateKeys",
                UsuarioAdmin = "tester",
                Resultado = true,
                Detalles = System.Text.Json.JsonSerializer.Serialize(detalles),
                Tipo = "Operación"
            };

            svc.RegistrarEvento(evento);

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Accion, Resultado, Detalles FROM Auditoria ORDER BY Id DESC LIMIT 1";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());

            Assert.Equal("export-delta", reader.GetString(0));
            Assert.Equal("OK", reader.GetString(1));
            var detallesStored = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            Assert.Contains("outfile", detallesStored);
        }

        [Fact]
        public void Restore_Event_Is_Stored_On_Success()
        {
            var svc = new AuditoriaService(_connectionString);

            var evento = new AuditoriaEvento
            {
                Accion = "restore",
                Modulo = "RotateKeys",
                UsuarioAdmin = "tester",
                Resultado = true,
                Detalles = System.Text.Json.JsonSerializer.Serialize(new { backup = "b.db", target = "t.db" }),
                Tipo = "Operación"
            };

            svc.RegistrarEvento(evento);

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Accion, Resultado, Detalles FROM Auditoria ORDER BY Id DESC LIMIT 1";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());

            Assert.Equal("restore", reader.GetString(0));
            Assert.Equal("OK", reader.GetString(1));
            var detallesStored = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            Assert.Contains("backup", detallesStored);
        }

        [Fact]
        public void ReplayDelta_Event_Is_Stored_On_Success()
        {
            var svc = new AuditoriaService(_connectionString);

            var evento = new AuditoriaEvento
            {
                Accion = "replay-delta",
                Modulo = "RotateKeys",
                UsuarioAdmin = "tester",
                Resultado = true,
                Detalles = System.Text.Json.JsonSerializer.Serialize(new { infile = "delta.json", count = 10, db = "Data Source=auditoria.db" }),
                Tipo = "Operación"
            };

            svc.RegistrarEvento(evento);

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Accion, Resultado, Detalles FROM Auditoria ORDER BY Id DESC LIMIT 1";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());

            Assert.Equal("replay-delta", reader.GetString(0));
            Assert.Equal("OK", reader.GetString(1));
            var detallesStored = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            Assert.Contains("infile", detallesStored);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
