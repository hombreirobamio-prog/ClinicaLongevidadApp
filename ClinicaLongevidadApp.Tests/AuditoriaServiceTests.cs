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
            cmd.CommandText = "SELECT EventId, PrevHash, Hash, Signature, Detalles, DetallesPlain, DetallesEnc FROM Auditoria ORDER BY Id DESC LIMIT 1";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());

            string eventId = reader.GetString(0);
            string prevHash = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            string hash = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            string signature = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            string detalles = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
            string detallesPlain = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
            string detallesEnc = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);

            Assert.False(string.IsNullOrWhiteSpace(eventId));
            Assert.False(string.IsNullOrWhiteSpace(hash));
            Assert.False(string.IsNullOrWhiteSpace(signature));
            string payload = detallesPlain;
            if (string.IsNullOrWhiteSpace(payload))
            {
                // try Detalles (may contain plain or encrypted blob)
                if (!string.IsNullOrWhiteSpace(detalles))
                {
                    payload = detalles;
                }
                else if (!string.IsNullOrWhiteSpace(detallesEnc))
                {
                    try
                    {
                        var encKey = new LocalKeyProvider().GetEncryptionKey();
                        var combined = Convert.FromBase64String(detallesEnc);
                        var nonce = new byte[12];
                        var tag = new byte[16];
                        var cipher = new byte[combined.Length - nonce.Length - tag.Length];
                        Buffer.BlockCopy(combined, 0, nonce, 0, nonce.Length);
                        Buffer.BlockCopy(combined, nonce.Length, tag, 0, tag.Length);
                        Buffer.BlockCopy(combined, nonce.Length + tag.Length, cipher, 0, cipher.Length);
                        var plain = new byte[cipher.Length];
                        // AesGcm(byte[]) is obsolete in .NET 8; explicitly allow the legacy constructor
#pragma warning disable SYSLIB0053
                        var keyForAes = encKey ?? Array.Empty<byte>();
                        using (var aesg = new System.Security.Cryptography.AesGcm(keyForAes))
                        {
                            aesg.Decrypt(nonce, cipher, tag, plain);
                        }
#pragma warning restore SYSLIB0053
                        payload = System.Text.Encoding.UTF8.GetString(plain);
                    }
                    catch { payload = detallesEnc; }
                }
            }

            Assert.Contains("foo", payload);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
