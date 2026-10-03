using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ClinicaLongevidadApp.Tests;

public sealed class UsuarioAtomicityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "usuario-atomic-" + Guid.NewGuid().ToString("N"));
    private readonly Keys keys = new();
    private readonly string? previousRole = Sesion.RolActual;
    private readonly string? previousUser = Sesion.UsuarioActual;
    private readonly string? previousAuth = Environment.GetEnvironmentVariable("AUDIT_ENFORCE_AUTH");
    private string DbPath => Path.Combine(root, "synthetic.db");
    private string ConnectionString => new SqliteConnectionStringBuilder
        { DataSource = DbPath, Pooling = false, ForeignKeys = true }.ToString();
    private readonly AuditoriaService audit;

    private sealed class Keys : IKeyProvider
    {
        public bool Available = true;
        private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
        public byte[] GetHmacKey() => Available ? key : Array.Empty<byte>();
        public string GetHmacKeyVersion() => "user-test-v1";
        public byte[]? GetHmacKeyByVersion(string? version) => version == GetHmacKeyVersion() ? key : null;
        public byte[]? GetEncryptionKey() => null;
        public string GetEncryptionKeyVersion() => "";
        public byte[]? GetEncryptionKeyByVersion(string? version) => null;
    }

    public UsuarioAtomicityTests()
    {
        Directory.CreateDirectory(root);
        using (var schema = new SQLite.SQLiteConnection(DbPath)) schema.CreateTable<Usuario>();
        audit = new AuditoriaService(ConnectionString, keys);
        Assert.True(audit.IsInitialized);
        Sesion.RolActual = "Administración";
        Environment.SetEnvironmentVariable("AUDIT_ENFORCE_AUTH", "1");
    }

    public void Dispose()
    {
        Sesion.RolActual = previousRole;
        Sesion.UsuarioActual = previousUser;
        Environment.SetEnvironmentVariable("AUDIT_ENFORCE_AUTH", previousAuth);
    }

    private object? Sql(string sql)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static Usuario NewUser() => new()
    {
        NombreUsuario = "synthetic-user", NombreCompleto = "Usuario sintético", Email = "test@example.invalid",
        TelefonoInterno = "123", PasswordHash = Guid.NewGuid().ToString("N"), Activo = true,
        Rol = "Recepción", Area = "Recepción", FechaCreacion = new DateTime(2000, 1, 1),
        UltimoAcceso = new DateTime(2026, 9, 30, 9, 15, 0)
    };

    private Usuario Read(int id)
    {
        using var connection = new SQLite.SQLiteConnection(DbPath);
        return connection.Find<Usuario>(id);
    }

    private void AssertEquivalent(Usuario expected)
    {
        var actual = Read(expected.Id);
        foreach (var property in typeof(Usuario).GetProperties())
            Assert.Equal(property.GetValue(expected), property.GetValue(actual));
    }

    private void AssertNoSecrets(params string[] secrets)
    {
        foreach (var item in audit.GetRecentAudits(100))
        {
            var json = JsonSerializer.Serialize(item);
            Assert.DoesNotContain("PBKDF2$", json);
            foreach (var secret in secrets) Assert.DoesNotContain(secret, json);
        }
    }

    [Fact]
    public void SuccessPreservesFieldsAndAuditsEachOperationWithoutSecrets()
    {
        var user = NewUser();
        var password = user.PasswordHash;
        var start = DateTime.Now;
        UsuarioService.Guardar(user, audit, DbPath);
        Assert.True(user.Id > 0);
        Assert.InRange(user.FechaCreacion, start, DateTime.Now);
        Assert.True(PasswordSecurity.VerifyPassword(password, user.PasswordHash));
        AssertEquivalent(user);
        var hash = user.PasswordHash;
        var createdAt = user.FechaCreacion;

        user.NombreCompleto = "Editado";
        user.Email = "edited@example.invalid";
        user.TelefonoInterno = "456";
        user.Activo = false;
        user.Rol = "Médico";
        user.Area = "Medicina General";
        user.UltimoAcceso = null;
        UsuarioService.Guardar(user, audit, DbPath);
        Assert.Equal(hash, user.PasswordHash);
        Assert.Equal(createdAt, user.FechaCreacion);
        AssertEquivalent(user);
        using (var payload = JsonDocument.Parse(audit.GetRecentAudits(1)[0].Detalles!))
        {
            Assert.Equal("Recepción", payload.RootElement.GetProperty("RolAnterior").GetString());
            Assert.Equal("Recepción", payload.RootElement.GetProperty("AreaAnterior").GetString());
        }

        var changedPassword = Guid.NewGuid().ToString("N");
        user.PasswordHash = changedPassword;
        UsuarioService.Guardar(user, audit, DbPath);
        Assert.True(PasswordSecurity.VerifyPassword(changedPassword, Read(user.Id).PasswordHash));
        using (var payload = JsonDocument.Parse(audit.GetRecentAudits(1)[0].Detalles!))
            Assert.Equal("True", payload.RootElement.GetProperty("PasswordCambiada").GetString());
        var beforeReset = Read(user.Id);
        var temporary = UsuarioService.RestablecerContraseña(user.Id, audit, DbPath);
        var afterReset = Read(user.Id);
        Assert.True(PasswordSecurity.VerifyPassword(temporary, afterReset.PasswordHash));
        foreach (var property in typeof(Usuario).GetProperties().Where(p => p.Name != nameof(Usuario.PasswordHash)))
            Assert.Equal(property.GetValue(beforeReset), property.GetValue(afterReset));
        UsuarioService.Eliminar(user.Id, audit, DbPath);
        Assert.Equal(0L, Sql("SELECT COUNT(*) FROM Usuario"));
        Assert.Equal(5L, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Equal("Usuario.Crear|Usuario.Actualizar|Usuario.Actualizar|Usuario.RestablecerContraseña|Usuario.Eliminar",
            Sql("SELECT group_concat(Accion,'|') FROM (SELECT Accion FROM Auditoria ORDER BY Id)"));
        Assert.Empty(audit.VerifyIntegrity());
        AssertNoSecrets(password, hash, changedPassword, temporary, afterReset.PasswordHash);
    }

    [Theory]
    [InlineData("create", "audit")]
    [InlineData("update", "audit")]
    [InlineData("delete", "audit")]
    [InlineData("reset", "audit")]
    [InlineData("create", "key")]
    [InlineData("update", "key")]
    [InlineData("delete", "key")]
    [InlineData("reset", "key")]
    [InlineData("create", "commit")]
    [InlineData("update", "commit")]
    [InlineData("delete", "commit")]
    [InlineData("reset", "commit")]
    [InlineData("create", "business")]
    [InlineData("update", "business")]
    [InlineData("delete", "business")]
    [InlineData("reset", "business")]
    public void FailureRollsBackBusinessAndAudit(string operation, string failure)
    {
        var user = NewUser();
        if (operation != "create") UsuarioService.Guardar(user, audit, DbPath);
        var originalJson = operation == "create" ? null : JsonSerializer.Serialize(Read(user.Id));
        var originalId = user.Id;
        var createdAt = user.FechaCreacion;
        var auditCount = Sql("SELECT COUNT(*) FROM Auditoria");
        if (failure == "key") keys.Available = false;
        else if (failure == "audit")
            Sql("CREATE TRIGGER reject_audit BEFORE INSERT ON Auditoria BEGIN SELECT RAISE(ABORT,'synthetic audit failure'); END;");
        else if (failure == "business")
        {
            var verb = operation == "create" ? "INSERT" : operation == "delete" ? "DELETE" : "UPDATE";
            Sql($"CREATE TRIGGER reject_business BEFORE {verb} ON Usuario BEGIN SELECT RAISE(ABORT,'synthetic business failure'); END;");
        }
        else
            Sql("CREATE TABLE Parent (Id INTEGER PRIMARY KEY); CREATE TABLE DeferredFailure (Id INTEGER REFERENCES Parent(Id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER reject_commit AFTER INSERT ON Auditoria BEGIN INSERT INTO DeferredFailure VALUES (123); END;");
        user.Rol = "Must not persist";
        user.PasswordHash = Guid.NewGuid().ToString("N");
        var pendingPassword = user.PasswordHash;
        string? returnedPassword = null;
        var error = Record.Exception(() =>
        {
            if (operation == "delete") UsuarioService.Eliminar(user.Id, audit, DbPath);
            else if (operation == "reset") returnedPassword = UsuarioService.RestablecerContraseña(user.Id, audit, DbPath);
            else UsuarioService.Guardar(user, audit, DbPath);
        });
        if (failure == "key") Assert.IsType<InvalidOperationException>(error);
        else Assert.IsType<SqliteException>(error);
        Assert.Null(returnedPassword);
        Assert.Equal(auditCount, Sql("SELECT COUNT(*) FROM Auditoria"));
        Assert.Equal(operation == "create" ? 0L : 1L, Sql("SELECT COUNT(*) FROM Usuario"));
        Assert.Equal(originalId, user.Id);
        Assert.Equal(createdAt, user.FechaCreacion);
        Assert.Equal(pendingPassword, user.PasswordHash);
        if (operation != "create") Assert.Equal(originalJson, JsonSerializer.Serialize(Read(originalId)));
        if (failure == "commit") Assert.Equal(0L, Sql("SELECT COUNT(*) FROM DeferredFailure"));
    }

    [Theory]
    [InlineData("missing-audit")]
    [InlineData("different-database")]
    [InlineData("missing-user")]
    public void InvalidContextRejectsAllOperations(string scenario)
    {
        AuditoriaService? service = scenario == "missing-audit" ? null : scenario == "different-database"
            ? new AuditoriaService(new SqliteConnectionStringBuilder
                { DataSource = Path.Combine(root, "other.db"), Pooling = false }.ToString(), keys)
            : audit;
        var user = NewUser();
        user.Id = 999;
        Assert.Throws<InvalidOperationException>(() => UsuarioService.Guardar(user, service, DbPath));
        Assert.Throws<InvalidOperationException>(() => UsuarioService.Eliminar(user.Id, service, DbPath));
        Assert.Throws<InvalidOperationException>(() => UsuarioService.RestablecerContraseña(user.Id, service, DbPath));
        if (scenario != "missing-user")
            Assert.Throws<InvalidOperationException>(() => UsuarioService.Guardar(NewUser(), service, DbPath));
        Assert.Equal(0L, Sql("SELECT COUNT(*) FROM Usuario"));
        Assert.Empty(audit.GetRecentAudits(10));
        if (service != null) Assert.Empty(service.GetRecentAudits(10));
    }

    [Fact]
    public void AuthorizationRejectsWritesAndAllowsSelfResetOnly()
    {
        var user = NewUser();
        UsuarioService.Guardar(user, audit, DbPath);
        var before = JsonSerializer.Serialize(Read(user.Id));
        Sesion.RolActual = "Recepcion";
        Sesion.UsuarioActual = "different-user";
        Assert.Throws<UnauthorizedAccessException>(() => UsuarioService.Guardar(NewUser(), audit, DbPath));
        Assert.Throws<UnauthorizedAccessException>(() => UsuarioService.Guardar(user, audit, DbPath));
        Assert.Throws<UnauthorizedAccessException>(() => UsuarioService.Eliminar(user.Id, audit, DbPath));
        Assert.Throws<UnauthorizedAccessException>(() => UsuarioService.RestablecerContraseña(user.Id, audit, DbPath));
        Assert.Equal(before, JsonSerializer.Serialize(Read(user.Id)));
        Assert.Single(audit.GetRecentAudits(10));
        Sesion.UsuarioActual = user.NombreUsuario;
        var temporary = UsuarioService.RestablecerContraseña(user.Id, audit, DbPath);
        Assert.True(PasswordSecurity.VerifyPassword(temporary, Read(user.Id).PasswordHash));
        Assert.Equal(2, audit.GetRecentAudits(10).Count);
        Assert.Empty(audit.VerifyIntegrity());
    }
}
