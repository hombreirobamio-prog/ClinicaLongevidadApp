using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class ServiceAuditoriaIntegrationTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly AuditoriaService? _auditoriaServiceBackup;

        public ServiceAuditoriaIntegrationTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"auditoria_service_test_{Guid.NewGuid():N}.db");
            _connectionString = $"Data Source={_dbPath}";
            // Ensure env key
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", "test-key-1234567890");

            // Backup existing App.AuditoriaService using reflection (private setter)
            var prop = typeof(App).GetProperty("AuditoriaService", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            _auditoriaServiceBackup = prop?.GetValue(null) as AuditoriaService;
        }

        [Fact]
        public void CitaService_Guardar_Crea_Auditoria()
        {
            var auditoria = new AuditoriaService(_connectionString);
            // Set App.AuditoriaService via its backing field since setter is private
            var backing = typeof(App).GetField("<AuditoriaService>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            backing?.SetValue(null, auditoria);

            var cita = new Cita
            {
                PacienteId = 42,
                PacienteNombre = "Paciente Test",
                Fecha = DateTime.Today,
                Hora = "09:30",
                Profesional = "Dr. Test",
                Estado = "Pendiente"
            };

            // Ensure a session role allowed to create appointments when authorization enforcement is active
            Sesion.RolActual = "Recepcion";
            CitaService.Guardar(cita);

            var recent = auditoria.GetRecentAudits(5);
            Assert.NotEmpty(recent);
            Assert.Equal("Cita.Crear", recent[0].Accion);
            Assert.Contains("Paciente Test", recent[0].UsuarioAfectado);
        }

        [Fact]
        public void FestivoService_Operaciones_Generan_Auditoria()
        {
            var auditoria = new AuditoriaService(_connectionString);
            var backing2 = typeof(App).GetField("<AuditoriaService>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            backing2?.SetValue(null, auditoria);

            var festivo = new Festivo
            {
                Fecha = new DateTime(2025, 1, 1),
                Nombre = "Año Nuevo",
                Tipo = "Nacional",
                Activo = true
            };

            // Crear (requires Administración when enforcement enabled)
            Sesion.RolActual = "Administración";
            FestivoService.Guardar(festivo);
            var recent = auditoria.GetRecentAudits(5);
            Assert.NotEmpty(recent);
            Assert.Equal("Festivo.Crear", recent[0].Accion);

            // Actualizar
            festivo.Nombre = "Año Nuevo Modificado";
            FestivoService.Guardar(festivo);
            recent = auditoria.GetRecentAudits(5);
            Assert.Equal("Festivo.Actualizar", recent[0].Accion);

            // Eliminar
            FestivoService.Eliminar(festivo.Id);
            recent = auditoria.GetRecentAudits(5);
            Assert.Equal("Festivo.Eliminar", recent[0].Accion);
        }

        public void Dispose()
        {
            // Restore App.AuditoriaService via backing field
            var backingRestore = typeof(App).GetField("<AuditoriaService>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            backingRestore?.SetValue(null, _auditoriaServiceBackup);
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
