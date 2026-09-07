using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class AuditoriaIntegrityTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public AuditoriaIntegrityTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"auditoria_integrity_{Guid.NewGuid():N}.db");
            _connectionString = $"Data Source={_dbPath}";
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", "integrity-test-key-1234567890");
            Environment.SetEnvironmentVariable("AUDIT_ENC_KEY", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("encryptionkey1234567890123456")));
        }

        [Fact]
        public void VerifyIntegrity_Detects_Tamper()
        {
            var service = new AuditoriaService(_connectionString);


            var evento1 = new AuditoriaEvento { UsuarioAdmin = "u1", Accion = "A1", Modulo = "M" , Detalles = "{\"a\":1}" };
            var evento2 = new AuditoriaEvento { UsuarioAdmin = "u2", Accion = "A2", Modulo = "M" , Detalles = "{\"b\":2}" };

            service.RegistrarEvento(evento1);
            service.RegistrarEvento(evento2);



            // Integrity should be OK
            var ok = service.VerifyIntegrity();
            Assert.Empty(ok);

            // Tamper with second row detalles
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Auditoria SET Detalles = 'tampered' WHERE Id = (SELECT MAX(Id) FROM Auditoria)";
                cmd.ExecuteNonQuery();
            }



            var errors = service.VerifyIntegrity();
            Assert.NotEmpty(errors);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }

    }
}
