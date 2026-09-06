using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.ViewModels;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class AuditoriaTests
    {
        [Fact]
        public async Task GuardarCita_Invoca_Auditoria()
        {
            // Arrange
            var mockPacienteService = new Mock<IPacienteService>();
            var mockCitaService = new Mock<ICitaService>();
            var mockAuditoria = new Mock<IAuditoriaService>();

            var paciente = new Paciente { Id = 10, NombreCompleto = "Test Paciente" };

            bool called = false;
            mockAuditoria.Setup(a => a.RegistrarEvento(It.IsAny<AuditoriaEvento>()))
                .Callback<AuditoriaEvento>(e => { called = true; });

            var vm = new PanelRecepcionViewModel(PanelRecepcionModo.Completo, mockPacienteService.Object, mockCitaService.Object, mockAuditoria.Object, autoInitialize: false);

            // Set paciente directly
            vm.PacienteSeleccionado = paciente;

            // Prepare viewmodel state for creating a new cita
            vm.FechaCita = DateTime.Today;
            vm.HoraCita = "10:00";
            vm.ProfesionalCita = "Dr. Test";
            vm.EstadoCita = "Pendiente";

            // Act
            await vm.AceptarCitaAsync();

            // Assert
            mockCitaService.Verify(s => s.Guardar(It.IsAny<Cita>()), Times.Once);
            mockAuditoria.Verify(a => a.RegistrarEvento(It.Is<AuditoriaEvento>(e => e.Accion.Contains("Cita"))), Times.Once);
        }

        [Fact]
        public async Task CambiarEstadoCita_Invoca_Auditoria()
        {
            // Arrange
            var mockPacienteService = new Mock<IPacienteService>();
            var mockCitaService = new Mock<ICitaService>();
            var mockAuditoria = new Mock<IAuditoriaService>();

            var cita = new Cita { Id = 5, PacienteId = 10, PacienteNombre = "Paciente X", Fecha = DateTime.Today, Hora = "11:00", Profesional = "Dr. Y", Estado = "Pendiente" };


            bool called = false;
            mockAuditoria.Setup(a => a.RegistrarEvento(It.IsAny<AuditoriaEvento>()))
                .Callback<AuditoriaEvento>(e => { called = true; });

            var vm = new PanelRecepcionViewModel(PanelRecepcionModo.Completo, mockPacienteService.Object, mockCitaService.Object, mockAuditoria.Object, autoInitialize: false);

            vm.CitaSeleccionada = cita;

            // Act
            await vm.ConfirmarCitaSeleccionadaAsync();

            // Assert
            mockCitaService.Verify(s => s.Guardar(It.Is<Cita>(c => c.Id == 5 && c.Estado == "Confirmada")), Times.Once);
            Assert.True(called, "Auditoría no fue invocada (flag)");
            mockAuditoria.Verify(a => a.RegistrarEvento(It.Is<AuditoriaEvento>(e => e.Accion == "Cita.Confirmar")), Times.Once);
        }
    }
}
