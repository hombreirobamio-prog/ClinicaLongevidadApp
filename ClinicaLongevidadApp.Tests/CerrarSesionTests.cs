using ClinicaLongevidadApp;
using ClinicaLongevidadApp.Services;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class CerrarSesionTests
    {
        [Fact]
        public void CerrarSesion_NoThrow_Y_BorraSesion()
        {
            // Arrange
            ClinicaLongevidadApp.Services.Sesion.UsuarioActual = "usuario_test";
            ClinicaLongevidadApp.Services.Sesion.RolActual = "rol";
            ClinicaLongevidadApp.Services.Sesion.AreaActual = "area";

            // Act
            ClinicaLongevidadApp.App.CerrarSesion();

            // Assert
            Assert.Null(ClinicaLongevidadApp.Services.Sesion.UsuarioActual);
            Assert.Null(ClinicaLongevidadApp.Services.Sesion.RolActual);
            Assert.Null(ClinicaLongevidadApp.Services.Sesion.AreaActual);
        }
    }
}
