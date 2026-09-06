using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.ViewModels;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class PanelRecepcionViewModelTests
    {
        [Fact]
        public async Task CargarPacientesAsync_Populates_PacientesCollection()
        {
            // Arrange
            var mockPacienteService = new Mock<IPacienteService>();
            var lista = new List<Paciente>
            {
                new Paciente { Id = 1, NombreCompleto = "Juan Perez" },
                new Paciente { Id = 2, NombreCompleto = "Ana Gomez" }
            };

            mockPacienteService.Setup(s => s.ObtenerTodos()).Returns(lista);

            var vm = new PanelRecepcionViewModel(PanelRecepcionModo.Completo, mockPacienteService.Object, autoInitialize: false);

            // Act
            await vm.CargarPacientesAsync();

            // Assert
            Assert.Equal(2, vm.Pacientes.Count);
            Assert.Contains(vm.Pacientes, p => p.NombreCompleto == "Juan Perez");
        }
    }
}
