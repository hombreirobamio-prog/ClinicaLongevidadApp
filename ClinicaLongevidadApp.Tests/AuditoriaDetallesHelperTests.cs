using ClinicaLongevidadApp.Helpers;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class AuditoriaDetallesHelperTests
    {
        [Theory]
        [InlineData("{\"PacienteId\":\"123\"}", "PacienteId", "123", true)]
        [InlineData("{\"Detalle\":\"abc123def\"}", "Detalle", "123", true)]
        [InlineData("{\"pacienteid\":\"ABC\"}", "PacienteId", "abc", true)]
        [InlineData("PacienteId=999;Otro=valor", "PacienteId", "999", true)]
        [InlineData("PacienteId=999;Detalle={malformed", "PacienteId", "999", true)]
        [InlineData("{\"Numero\":1}", "Numero", "1", true)]
        [InlineData("{\"Obj\":{\"Id\":\"5\"}}", "Obj", "Id", false)] // no coincide con campo "Obj" por ToString() distinto
        [InlineData("", "PacienteId", "123", false)]
        [InlineData(null, "PacienteId", "123", false)]
        public void CoincideCampo_VariosCasos(string? detalles, string campo, string valor, bool esperado)
        {
            bool resultado = AuditoriaDetallesHelper.CoincideCampo(detalles, campo, valor);
            Assert.Equal(esperado, resultado);
        }

        [Fact]
        public void CoincideCampo_NoCoincideDevuelveFalse()
        {
            string detalles = "{\"PacienteId\":\"123\"}";
            Assert.False(AuditoriaDetallesHelper.CoincideCampo(detalles, "PacienteId", "999"));
            Assert.False(AuditoriaDetallesHelper.CoincideCampo("Clave=Valor;Otra=1", "PacienteId", "1"));
        }

        [Fact]
        public void CoincideCampo_FormatoLegado_CasoInsensible()
        {
            string detalles = "pacienteid=ABC;otro=Z";
            Assert.True(AuditoriaDetallesHelper.CoincideCampo(detalles, "PacienteId", "abc"));
        }
    }
}