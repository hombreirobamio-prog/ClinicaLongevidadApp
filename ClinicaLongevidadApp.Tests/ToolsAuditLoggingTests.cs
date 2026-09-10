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

            var detallesObj = new { backup = "backup.db", outfile = "delta.json", rows = 5, sourceDb = "Data Source=auditoria.db" };

            var evento = new AuditoriaEvento
            {
                Accion = "export-delta",
                Modulo = "RotateKeys",
                UsuarioAdmin = "tester",
                Resultado = true,
                Detalles = System.Text.Json.JsonSerializer.Serialize(detallesObj),
                Tipo = "Operación"
            };

            svc.RegistrarEvento(evento);

            var auditoria = new AuditoriaService(_connectionString);
            var recent = auditoria.GetRecentAudits(5);
            Assert.NotEmpty(recent);
            Assert.Equal("export-delta", recent[0].Accion);
            Assert.Contains("outfile", recent[0].Detalles ?? string.Empty);
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

            var auditoria2 = new AuditoriaService(_connectionString);
            var recent2 = auditoria2.GetRecentAudits(5);
            Assert.NotEmpty(recent2);
            Assert.Equal("restore", recent2[0].Accion);
            Assert.Contains("backup", recent2[0].Detalles ?? string.Empty);
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

            var auditoria3 = new AuditoriaService(_connectionString);
            var recent3 = auditoria3.GetRecentAudits(5);
            Assert.NotEmpty(recent3);
            Assert.Equal("replay-delta", recent3[0].Accion);
            Assert.Contains("infile", recent3[0].Detalles ?? string.Empty);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
