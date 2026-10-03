using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using System;
using System.IO;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class UsuarioServiceAuditoriaTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly AuditoriaService? _auditoriaServiceBackup;

        public UsuarioServiceAuditoriaTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"auditoria_usuario_test_{Guid.NewGuid():N}.db");
            _connectionString = $"Data Source={_dbPath}";

            // Ensure HMAC key env for AuditoriaService
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", "test-key-1234567890");

            // Backup current App.AuditoriaService via reflection
            var prop = typeof(App).GetProperty("AuditoriaService", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            _auditoriaServiceBackup = prop?.GetValue(null) as AuditoriaService;

            // Assign test auditoria instance to App backing field
            var auditoria = new AuditoriaService(_connectionString);
            var backing = typeof(App).GetField("<AuditoriaService>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            backing?.SetValue(null, auditoria);
        }

        [Fact]
        public void GuardarUsuario_Crea_Auditoria()
        {
            var usuario = new Usuario
            {
                NombreUsuario = "usuario_test",
                NombreCompleto = "Usuario Test",
                Email = "u@test.local",
                PasswordHash = "plaintextpwd",
                Rol = "Recepcion",
                Area = "Area1",
                Activo = true
            };

            // Ensure admin when authorization enforcement might be active
            Sesion.RolActual = "Administración";
            UsuarioService.Guardar(usuario, App.AuditoriaService, _dbPath);

            // Read audits
            var auditoria = new AuditoriaService(_connectionString);
            var recent = auditoria.GetRecentAudits(5);
            Assert.NotEmpty(recent);
            Assert.Equal("Usuario.Crear", recent[0].Accion);
            Assert.Equal("usuario_test", recent[0].UsuarioAfectado);
        }

        [Fact]
        public void ValidarLogin_NoEncontrado_Genera_Auditoria()
        {
            // Ensure no such user exists
            var ok = UsuarioService.ValidarLogin("noexiste", "whatever");
            Assert.False(ok);

            // Use AuditoriaService helper which will prefer DetallesPlain or attempt decryption of DetallesEnc
            var auditoria = new AuditoriaService(_connectionString);
            var recent = auditoria.GetRecentAudits(5);
            Assert.NotEmpty(recent);
            Assert.Equal("Usuario.Login", recent[0].Accion);
            Assert.Contains("NoEncontrado", recent[0].Detalles ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RestablecerContraseña_Genera_Auditoria()
        {
            var usuario = new Usuario
            {
                NombreUsuario = "usuario_reset",
                NombreCompleto = "Usuario Reset",
                Email = "r@test.local",
                PasswordHash = "plaintextpwd",
                Activo = true
            };

            // Ensure admin when authorization enforcement might be active
            Sesion.RolActual = "Administración";
            UsuarioService.Guardar(usuario, App.AuditoriaService, _dbPath);

            // Reload to get id
            var saved = usuario;
            Assert.NotNull(saved);

            var nueva = UsuarioService.RestablecerContraseña(saved!.Id, App.AuditoriaService, _dbPath);
            Assert.False(string.IsNullOrWhiteSpace(nueva));

            var auditoria2 = new AuditoriaService(_connectionString);
            var recent2 = auditoria2.GetRecentAudits(5);
            Assert.NotEmpty(recent2);
            Assert.Equal("Usuario.RestablecerContraseña", recent2[0].Accion);
            // Detalles is JSON; parse and verify the Nota contains expected text
            var detallesText = recent2[0].Detalles ?? string.Empty;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(detallesText);
                if (doc.RootElement.TryGetProperty("Nota", out var nota))
                {
                    Assert.Contains("Contrase", nota.GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    Assert.Contains("Contrase", detallesText, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                Assert.Contains("Contrase", detallesText, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void EliminarUsuario_Genera_Auditoria()
        {
            var usuario = new Usuario
            {
                NombreUsuario = "usuario_delete",
                NombreCompleto = "Usuario Delete",
                Email = "d@test.local",
                PasswordHash = "plaintextpwd",
                Activo = true
            };

            // Ensure admin when authorization enforcement might be active
            Sesion.RolActual = "Administración";
            UsuarioService.Guardar(usuario, App.AuditoriaService, _dbPath);
            var saved = usuario;
            Assert.NotNull(saved);

            UsuarioService.Eliminar(saved!.Id, App.AuditoriaService, _dbPath);

            var auditoria3 = new AuditoriaService(_connectionString);
            var recent3 = auditoria3.GetRecentAudits(5);
            Assert.NotEmpty(recent3);
            Assert.Equal("Usuario.Eliminar", recent3[0].Accion);
            Assert.Contains(saved.Id.ToString(), recent3[0].Detalles ?? recent3[0].UsuarioAfectado);
        }

        public void Dispose()
        {
            // Restore App.AuditoriaService backing field
            var backingRestore = typeof(App).GetField("<AuditoriaService>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            backingRestore?.SetValue(null, _auditoriaServiceBackup);

            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
