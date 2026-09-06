using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.ViewModels;
using Moq;
using System.Threading.Tasks;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class GuardarDatosAuditoriaTests
    {
        [Fact]
        public async Task GuardarDatos_Invoca_PacienteService_y_Auditoria()
        {
            // Arrange
            var mockPacienteService = new Mock<IPacienteService>();
            var mockCitaService = new Mock<ICitaService>();
            var mockAuditoria = new Mock<IAuditoriaService>();

            bool auditCalled = false;
            mockAuditoria.Setup(a => a.RegistrarEvento(It.IsAny<AuditoriaEvento>()))
                .Callback<AuditoriaEvento>(e => auditCalled = true);

            var vm = new PanelRecepcionViewModel(PanelRecepcionModo.Completo, mockPacienteService.Object, mockCitaService.Object, mockAuditoria.Object, autoInitialize: false);

            // prepare patient data
            vm.Nombre = "Test Nombre";
            vm.Telefono = "123";
            vm.Email = "a@b.com";
            vm.MostrarDatosAdicionales = true;

            // Act
            await vm.GuardarDatosAsync();

            // Assert
            mockPacienteService.Verify(s => s.Guardar(It.IsAny<Paciente>()), Times.Once);
            Assert.True(auditCalled, "Auditoría no fue invocada");
        }
    }
}
