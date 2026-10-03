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
            Environment.SetEnvironmentVariable("AUDIT_ENC_KEY", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef")));
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

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void VerifyIntegrity_DetectsDeletedPredecessor(int deletedId)
        {
            var service = new AuditoriaService(_connectionString);
            for (var i = 0; i < 3; i++)
                service.RegistrarEvento(new AuditoriaEvento { UsuarioAdmin = "tester", Accion = "test", Modulo = "Tests" });
            Assert.Empty(service.VerifyIntegrity());

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Auditoria WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", deletedId);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            Assert.Contains(service.VerifyIntegrity(), error => error.Contains("previous hash"));
        }

        [Fact]
        public void VerifyIntegrity_ReportsUnavailableHistoricalKey()
        {
            var provider = new Moq.Mock<IKeyProvider>();
            var key = System.Text.Encoding.UTF8.GetBytes("synthetic-key-for-regression-test");
            provider.Setup(p => p.GetHmacKey()).Returns(key);
            provider.Setup(p => p.GetHmacKeyVersion()).Returns("v1");
            provider.Setup(p => p.GetHmacKeyByVersion("v1")).Returns(key);
            var service = new AuditoriaService(_connectionString, provider.Object);
            service.RegistrarEvento(new AuditoriaEvento { UsuarioAdmin = "tester", Accion = "test" });
            Assert.Empty(service.VerifyIntegrity());
            provider.Setup(p => p.GetHmacKeyByVersion("v1")).Returns((byte[]?)null);
            Assert.Contains(service.VerifyIntegrity(), error => error.Contains("unverifiable"));
        }

        [Fact]
        public void RegistrarEvento_ReportsStorageFailure()
        {
            var service = new AuditoriaService(_connectionString);
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TRIGGER reject_insert BEFORE INSERT ON Auditoria BEGIN SELECT RAISE(ABORT, 'synthetic failure'); END;";
            cmd.ExecuteNonQuery();
            Assert.Throws<SqliteException>(() => service.RegistrarEvento(new AuditoriaEvento { Accion = "test" }));
            cmd.CommandText = "SELECT COUNT(*) FROM Auditoria";
            Assert.Equal(0L, (long)cmd.ExecuteScalar()!);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }

    }
}
