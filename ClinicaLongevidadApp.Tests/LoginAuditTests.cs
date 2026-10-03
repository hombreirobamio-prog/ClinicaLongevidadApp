using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.ViewModels;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ClinicaLongevidadApp.Tests;

public sealed class LoginAuditTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "login-audit-" + Guid.NewGuid().ToString("N") + ".db");
    private readonly Keys keys = new();
    private readonly AuditoriaService audit;
    private readonly string password = Guid.NewGuid().ToString("N");
    private readonly Usuario user;
    private readonly string? previousUser = Sesion.UsuarioActual;
    private readonly string? previousRole = Sesion.RolActual;
    private readonly string? previousArea = Sesion.AreaActual;
    private int opened;
    private int lookups;
    private int notifications;
    private bool auditPresentAtNotification;
    private string ConnectionString => new SqliteConnectionStringBuilder
        { DataSource = path, Pooling = false, ForeignKeys = true }.ToString();

    private sealed class Keys : IKeyProvider
    {
        public bool Available = true;
        private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
        public byte[] GetHmacKey() => Available ? key : Array.Empty<byte>();
        public string GetHmacKeyVersion() => "login-test-v1";
        public byte[]? GetHmacKeyByVersion(string? version) => version == GetHmacKeyVersion() ? key : null;
        public byte[]? GetEncryptionKey() => null;
        public string GetEncryptionKeyVersion() => "";
        public byte[]? GetEncryptionKeyByVersion(string? version) => null;
    }

    public LoginAuditTests()
    {
        audit = new AuditoriaService(ConnectionString, keys);
        Assert.True(audit.IsInitialized);
        user = new Usuario
        {
            NombreUsuario = Guid.NewGuid().ToString("N"), PasswordHash = PasswordSecurity.HashPassword(password),
            Rol = "Recepción", Area = "Recepción", Activo = true
        };
        Sesion.Limpiar();
        Sesion.SessionChanged += OnSessionChanged;
    }

    private void OnSessionChanged()
    {
        notifications++;
        auditPresentAtNotification = audit.GetRecentAudits(1).Any(e => e.Accion == "Login.Correcto");
    }

    public void Dispose()
    {
        Sesion.SessionChanged -= OnSessionChanged;
        Sesion.UsuarioActual = previousUser;
        Sesion.RolActual = previousRole;
        Sesion.AreaActual = previousArea;
    }

    private LoginViewModel Create(string area = "Recepción", bool found = true) =>
        new(area, audit, name => { lookups++; return found ? user : null; }, _ => opened++)
        { NombreUsuario = user.NombreUsuario, PasswordHash = password };

    private void Sql(string sql)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void AssertDenied()
    {
        Assert.Null(Sesion.UsuarioActual);
        Assert.Null(Sesion.RolActual);
        Assert.Null(Sesion.AreaActual);
        Assert.Equal(0, opened);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void SuccessCommitsAuditBeforePublishingSessionAndNavigation()
    {
        Create().EntrarCommand.Execute(null);
        Assert.Equal(user.NombreUsuario, Sesion.UsuarioActual);
        Assert.Equal(user.Rol, Sesion.RolActual);
        Assert.Equal(user.Area, Sesion.AreaActual);
        Assert.Equal(1, opened);
        Assert.Equal(1, notifications);
        Assert.True(auditPresentAtNotification);
        var item = Assert.Single(audit.GetRecentAudits(10));
        Assert.Equal("Login.Correcto", item.Accion);
        Assert.Equal(user.Rol, item.Rol);
        Assert.Equal(user.Area, item.Area);
        var json = JsonSerializer.Serialize(item);
        Assert.DoesNotContain(password, json);
        Assert.DoesNotContain(user.PasswordHash, json);
        Assert.Empty(audit.VerifyIntegrity());
    }

    [Theory]
    [InlineData("key")]
    [InlineData("insert")]
    [InlineData("commit")]
    public void AuditFailurePreventsSessionAndAllowsRetry(string failure)
    {
        if (failure == "key") keys.Available = false;
        else if (failure == "insert")
            Sql("CREATE TRIGGER reject_audit BEFORE INSERT ON Auditoria BEGIN SELECT RAISE(ABORT,'synthetic audit failure'); END;");
        else
            Sql("CREATE TABLE Parent (Id INTEGER PRIMARY KEY); CREATE TABLE DeferredFailure (Id INTEGER REFERENCES Parent(Id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER reject_audit AFTER INSERT ON Auditoria BEGIN INSERT INTO DeferredFailure VALUES (123); END;");
        var vm = Create();
        vm.EntrarCommand.Execute(null);
        AssertDenied();
        Assert.Empty(audit.GetRecentAudits(10));
        if (failure == "key") keys.Available = true;
        else Sql("DROP TRIGGER reject_audit;");
        vm.EntrarCommand.Execute(null);
        Assert.Equal(1, opened);
        Assert.Equal(1, notifications);
        Assert.True(auditPresentAtNotification);
        Assert.Single(audit.GetRecentAudits(10));
        Assert.Empty(audit.VerifyIntegrity());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("password")]
    [InlineData("area")]
    public void DeniedLoginRecordsOneFailureWithoutPublishingSession(string reason)
    {
        if (reason == "inactive") user.Activo = false;
        var vm = Create(reason == "area" ? "Administración" : "Recepción", reason != "missing");
        if (reason == "password") vm.PasswordHash = "incorrect synthetic password";
        vm.EntrarCommand.Execute(null);
        AssertDenied();
        var item = Assert.Single(audit.GetRecentAudits(10));
        Assert.Equal(reason == "area" ? "Login.AccesoNoAutorizadoArea" : "Login.FallidoCredenciales", item.Accion);
        Assert.Equal("ERROR", item.Resultado);
        Assert.Empty(audit.VerifyIntegrity());
    }

    [Fact]
    public void FailedAuditDoesNotBypassLockoutAndNamesAreNormalized()
    {
        keys.Available = false;
        var vm = Create();
        vm.PasswordHash = "incorrect synthetic password";
        for (int i = 0; i < 5; i++) vm.EntrarCommand.Execute(null);
        keys.Available = true;
        vm.NombreUsuario = " " + user.NombreUsuario.ToUpperInvariant() + " ";
        vm.PasswordHash = password;
        vm.EntrarCommand.Execute(null);
        AssertDenied();
        Assert.Equal(5, lookups);
        Assert.Empty(audit.GetRecentAudits(10));
    }

    [Fact]
    public void SuccessfulLoginClearsPreviousFailedAttempts()
    {
        var vm = Create();
        vm.PasswordHash = "incorrect synthetic password";
        for (int i = 0; i < 4; i++) vm.EntrarCommand.Execute(null);
        vm.PasswordHash = password;
        vm.EntrarCommand.Execute(null);
        Assert.Equal(1, opened);
        vm.PasswordHash = "incorrect synthetic password";
        vm.EntrarCommand.Execute(null);
        vm.PasswordHash = password;
        vm.EntrarCommand.Execute(null);
        Assert.Equal(2, opened);
        Assert.Equal(7, lookups);
    }

    [Fact]
    public void MissingAuditServiceIsRejectedBeforeLogin()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LoginViewModel("Recepción", null!, _ => user, _ => opened++));
        AssertDenied();
    }
}

