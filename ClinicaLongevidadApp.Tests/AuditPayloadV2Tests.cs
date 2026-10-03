using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using Moq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ClinicaLongevidadApp.Tests;

public class AuditPayloadV2Tests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"audit-v2-{Guid.NewGuid():N}.db");
    private readonly Mock<IKeyProvider> keys = new();
    private readonly byte[] hmac = Encoding.UTF8.GetBytes("synthetic-signing-key-for-v2-tests");
    private string Connection => $"Data Source={path};Pooling=False";

    public AuditPayloadV2Tests()
    {
        keys.Setup(k => k.GetHmacKey()).Returns(hmac);
        keys.Setup(k => k.GetHmacKeyVersion()).Returns("v1");
        keys.Setup(k => k.GetHmacKeyByVersion("v1")).Returns(hmac);
        keys.Setup(k => k.GetEncryptionKey()).Returns(new byte[32]);
        keys.Setup(k => k.GetEncryptionKeyVersion()).Returns("enc-v1");
    }

    private void Execute(string sql)
    {
        using var conn = new SqliteConnection(Connection);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    [Theory]
    [InlineData("Rol")]
    [InlineData("Area")]
    [InlineData("SesionId")]
    [InlineData("Equipo")]
    [InlineData("VersionApp")]
    [InlineData("Tipo")]
    [InlineData("KeyVersionEnc")]
    [InlineData("DetallesEnc")]
    [InlineData("DetallesPlain")]
    public void MetadataTamperingInvalidatesSignature(string column)
    {
        var service = new AuditoriaService(Connection, keys.Object);
        service.RegistrarEvento(new AuditoriaEvento { Accion = "test", Detalles = "synthetic detail" });
        Assert.Empty(service.VerifyIntegrity());
        Execute($"UPDATE Auditoria SET {column} = 'tampered'");
        Assert.Contains(service.VerifyIntegrity(), e => e.Contains("signature mismatch"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(99)]
    public void VersionDowngradeOrUnknownVersionIsRejected(int version)
    {
        var service = new AuditoriaService(Connection, keys.Object);
        service.RegistrarEvento(new AuditoriaEvento { Accion = "test" });
        Execute($"UPDATE Auditoria SET PayloadVersion = {version}");
        Assert.NotEmpty(service.VerifyIntegrity());
    }

    [Theory]
    [InlineData("Production", 0)]
    [InlineData("", 0)]
    [InlineData("Production", 17)]
    [InlineData("Test", 17)]
    public void MissingOrInvalidEncryptionDoesNotPersistDetails(string environment, int size)
    {
        var old = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", environment);
            keys.Setup(k => k.GetEncryptionKey()).Returns(size == 0 ? null : new byte[size]);
            var service = new AuditoriaService(Connection, keys.Object);
            Assert.ThrowsAny<Exception>(() => service.RegistrarEvento(new AuditoriaEvento { Detalles = "synthetic secret" }));
            Assert.Empty(service.GetRecentAudits());
        }
        finally { Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", old); }
    }

    [Fact]
    public void ProductionStoresEncryptedDetailsWithoutPlainCopy()
    {
        var old = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
            var service = new AuditoriaService(Connection, keys.Object);
            service.RegistrarEvento(new AuditoriaEvento { Detalles = "synthetic secret" });
            Assert.Empty(service.VerifyIntegrity());
            using var conn = new SqliteConnection(Connection);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Detalles, DetallesPlain, DetallesEnc FROM Auditoria";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.True(reader.IsDBNull(1));
            Assert.Equal(reader.GetString(0), reader.GetString(2));
            var bytes = Convert.FromBase64String(reader.GetString(2));
            var plain = new byte[bytes.Length - 28];
            using var aes = new AesGcm(new byte[32], 16);
            aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), plain);
            Assert.Equal("synthetic secret", Encoding.UTF8.GetString(plain));
        }
        finally { Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", old); }
    }

    [Fact]
    public void LegacyRowAndNewRowVerifyTogetherWithoutRewritingLegacy()
    {
        var service = new AuditoriaService(Connection, keys.Object);
        // Independently construct an original-format record, without calling the v2 writer.
        var payload = new { EventId = "legacy-id", UsuarioAdmin = "tester", Accion = "legacy", FechaHora = "2026-09-01T00:00:00.0000000Z", Modulo = "Tests", UsuarioAfectado = "", Resultado = "OK", Detalles = "legacy" };
        var json = JsonSerializer.Serialize(payload);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("|" + json)));
        var signature = Convert.ToHexString(HMACSHA256.HashData(hmac, Encoding.UTF8.GetBytes(json)));
        using (var conn = new SqliteConnection(Connection))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Auditoria (EventId, UsuarioAdmin, Accion, Fechahora, Modulo, UsuarioAfectado, Resultado, Detalles, PrevHash, Hash, Signature, KeyVersion) VALUES ('legacy-id', 'tester', 'legacy', '2026-09-01T00:00:00.0000000Z', 'Tests', '', 'OK', 'legacy', '', @hash, @signature, 'v1')";
            cmd.Parameters.AddWithValue("@hash", hash);
            cmd.Parameters.AddWithValue("@signature", signature);
            cmd.ExecuteNonQuery();
        }
        Assert.Empty(service.VerifyIntegrity());
        Execute("ALTER TABLE Auditoria DROP COLUMN PayloadVersion");
        var before = File.ReadAllBytes(path);
        var readOnly = new SqliteConnectionStringBuilder(Connection) { Mode = SqliteOpenMode.ReadOnly }.ToString();
        var verifier = new AuditoriaService(readOnly, keys.Object, initializeSchema: false);
        Assert.True(verifier.IsInitialized);
        Assert.Empty(verifier.VerifyIntegrity());
        Assert.Equal(before, File.ReadAllBytes(path));
        service = new AuditoriaService(Connection, keys.Object);
        Assert.True(service.IsInitialized);
        Assert.Empty(service.VerifyIntegrity());
        service.RegistrarEvento(new AuditoriaEvento { Accion = "new" });
        Assert.Empty(service.VerifyIntegrity());
    }

    public void Dispose() { SqliteConnection.ClearAllPools(); if (File.Exists(path)) File.Delete(path); }
}
