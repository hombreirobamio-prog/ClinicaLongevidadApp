using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using System;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class AuthorizationEnforcementTests : IDisposable
    {
        private readonly string? _prevEnv;
        private readonly string _userDbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"usuario_auth_{Guid.NewGuid():N}.db");
        private readonly AuditoriaService _userAudit;

        public AuthorizationEnforcementTests()
        {
            _userAudit = new AuditoriaService(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                { DataSource = _userDbPath, Pooling = false }.ToString());
            Assert.True(_userAudit.IsInitialized);
            _prevEnv = Environment.GetEnvironmentVariable("AUDIT_ENFORCE_AUTH");
            Environment.SetEnvironmentVariable("AUDIT_ENFORCE_AUTH", "1");
            // Ensure clean session
            Sesion.Limpiar();
        }

        [Fact]
        public void Usuario_Guardar_Throws_When_Not_Admin()
        {
            Sesion.RolActual = "Recepcion";
            var u = new Usuario
            {
                NombreUsuario = "auth_test_user",
                NombreCompleto = "Auth Test",
                PasswordHash = "p",
                Activo = true
            };

            Assert.Throws<UnauthorizedAccessException>(() => UsuarioService.Guardar(u, _userAudit, _userDbPath));
        }

        [Fact]
        public void Usuario_Guardar_Succeeds_For_Admin()
        {
            Sesion.RolActual = "Administración";
            var u = new Usuario
            {
                NombreUsuario = "auth_admin_user",
                NombreCompleto = "Auth Admin",
                PasswordHash = "p",
                Activo = true
            };

            // should not throw
            UsuarioService.Guardar(u, _userAudit, _userDbPath);
        }

        [Fact]
        public void RestablecerPassword_Throws_When_Not_Admin_Or_Self()
        {
            // create user (must be created by admin when enforcement enabled)
            Sesion.RolActual = "Administración";
            var u = new Usuario
            {
                NombreUsuario = "u_reset",
                NombreCompleto = "U Reset",
                PasswordHash = "p",
                Activo = true
            };
            UsuarioService.Guardar(u, _userAudit, _userDbPath);
            var saved = u;
            Assert.NotNull(saved);

            // set session to different non-admin user
            Sesion.UsuarioActual = "otro";
            Sesion.RolActual = "Recepcion";

            Assert.Throws<UnauthorizedAccessException>(() => UsuarioService.RestablecerContraseña(saved!.Id, _userAudit, _userDbPath));
        }

        [Fact]
        public void RestablecerPassword_Allows_Self()
        {
            // create user as admin
            Sesion.RolActual = "Administración";
            var u = new Usuario
            {
                NombreUsuario = "u_self",
                NombreCompleto = "U Self",
                PasswordHash = "p",
                Activo = true
            };
            UsuarioService.Guardar(u, _userAudit, _userDbPath);
            var saved = u;
            Assert.NotNull(saved);

            Sesion.UsuarioActual = "u_self";
            Sesion.RolActual = "Recepcion";

            var nueva = UsuarioService.RestablecerContraseña(saved!.Id, _userAudit, _userDbPath);
            Assert.False(string.IsNullOrWhiteSpace(nueva));
        }

        [Fact]
        public void Cita_Guardar_Requires_Recepcion_Or_Admin()
        {
            var cita = new Cita
            {
                PacienteId = 1,
                PacienteNombre = "P",
                Fecha = DateTime.Today,
                Hora = "10:00",
                Profesional = "Dr",
                Estado = "Pendiente"
            };

            var dbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cita_auth_{Guid.NewGuid():N}.db");
            var auditoria = new AuditoriaService(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                { DataSource = dbPath, Pooling = false }.ToString());
            Assert.True(auditoria.IsInitialized);
            Sesion.RolActual = "Otro";
            Assert.Throws<UnauthorizedAccessException>(() => CitaService.Guardar(cita, auditoria, dbPath));

            Sesion.RolActual = "Recepcion";
            // should not throw
            CitaService.Guardar(cita, auditoria, dbPath);
        }

        [Fact]
        public void Festivo_Operations_Require_Admin()
        {
            var fest = new Festivo
            {
                Fecha = DateTime.Today,
                Nombre = "F",
                Tipo = "Nacional",
                Activo = true
            };

            var dbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"festivo_auth_{Guid.NewGuid():N}.db");
            var auditoria = new AuditoriaService(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                { DataSource = dbPath, Pooling = false }.ToString());
            Assert.True(auditoria.IsInitialized);

            Sesion.RolActual = "Recepcion";
            Assert.Throws<UnauthorizedAccessException>(() => FestivoService.Guardar(fest, auditoria, dbPath));
            Assert.Throws<UnauthorizedAccessException>(() => FestivoService.Eliminar(1, auditoria, dbPath));

            Sesion.RolActual = "Administración";
            FestivoService.Guardar(fest, auditoria, dbPath);
            Assert.True(fest.Id > 0);
            FestivoService.Eliminar(fest.Id, auditoria, dbPath);
            Assert.Equal(2, auditoria.GetRecentAudits(10).Count);
        }

        public void Dispose()
        {
            // restore env
            Environment.SetEnvironmentVariable("AUDIT_ENFORCE_AUTH", _prevEnv);
            Sesion.Limpiar();
        }
    }
}
