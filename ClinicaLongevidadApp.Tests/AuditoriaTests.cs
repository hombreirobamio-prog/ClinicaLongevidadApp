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
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GuardarCita_Delega_Auditoria_En_Servicio(bool fallaGuardado)
        {
            // Arrange
            var mockPacienteService = new Mock<IPacienteService>();
            var mockCitaService = new Mock<ICitaService>();
            var mockAuditoria = new Mock<IAuditoriaService>();
            if (fallaGuardado)
                mockCitaService.Setup(s => s.Guardar(It.IsAny<Cita>()))
                    .Throws(new InvalidOperationException("Fallo sintético de persistencia/auditoría"));

            var paciente = new Paciente { Id = 10, NombreCompleto = "Test Paciente" };

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
            if (fallaGuardado) Assert.Equal("10:00", vm.HoraCita);
            mockAuditoria.Verify(a => a.RegistrarEvento(It.IsAny<AuditoriaEvento>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CambiarEstadoCita_Delega_Auditoria_En_Servicio(bool fallaGuardado)
        {
            // Arrange
            var mockPacienteService = new Mock<IPacienteService>();
            var mockCitaService = new Mock<ICitaService>();
            var mockAuditoria = new Mock<IAuditoriaService>();
            if (fallaGuardado)
                mockCitaService.Setup(s => s.Guardar(It.IsAny<Cita>()))
                    .Throws(new InvalidOperationException("Fallo sintético de persistencia/auditoría"));

            var cita = new Cita { Id = 5, PacienteId = 10, PacienteNombre = "Paciente X", Fecha = DateTime.Today, Hora = "11:00", Profesional = "Dr. Y", Estado = "Pendiente" };

            var vm = new PanelRecepcionViewModel(PanelRecepcionModo.Completo, mockPacienteService.Object, mockCitaService.Object, mockAuditoria.Object, autoInitialize: false);

            vm.CitaSeleccionada = cita;

            // Act
            await vm.ConfirmarCitaSeleccionadaAsync();
            if (fallaGuardado)
            {
                Assert.Equal("Pendiente", cita.Estado);
                Assert.Same(cita, vm.CitaSeleccionada);
            }

            // Assert
            mockCitaService.Verify(s => s.Guardar(It.Is<Cita>(c => c.Id == 5 && c.Estado == "Confirmada")), Times.Once);
            mockAuditoria.Verify(a => a.RegistrarEvento(It.IsAny<AuditoriaEvento>()), Times.Never);
        }
    }
}
