using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.ViewModels;
using Moq;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace ClinicaLongevidadApp.Tests;

public class GuardarDatosAuditoriaTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardarDatos_DelegaGuardadoAuditadoSinEmitirExitosSeparados(bool failSave)
    {
        var patientService = new Mock<IPacienteService>();
        var appointmentService = new Mock<ICitaService>();
        var audit = new Mock<IAuditoriaService>();
        if (failSave) patientService.Setup(s => s.Guardar(It.IsAny<Paciente>())).Throws(new InvalidOperationException("Synthetic save failure"));
        var vm = new PanelRecepcionViewModel(PanelRecepcionModo.Completo, patientService.Object, appointmentService.Object, audit.Object, autoInitialize: false)
        {
            Nombre = "Paciente sintético", Telefono = "000", Email = "test@example.invalid", MostrarDatosAdicionales = true
        };
        await vm.GuardarDatosAsync();
        patientService.Verify(s => s.Guardar(It.IsAny<Paciente>()), Times.Once);
        audit.Verify(a => a.RegistrarEvento(It.IsAny<AuditoriaEvento>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CrearPacienteParaCita_NoDuplicaAuditoriaNiContinuaSiFallaGuardado(bool failSave)
    {
        var patientService = new Mock<IPacienteService>();
        var audit = new Mock<IAuditoriaService>();
        if (failSave) patientService.Setup(s => s.Guardar(It.IsAny<Paciente>())).Throws(new InvalidOperationException("Synthetic save failure"));
        else patientService.Setup(s => s.Guardar(It.IsAny<Paciente>())).Callback<Paciente>(p => p.Id = 1);
        var vm = new PanelRecepcionViewModel(PanelRecepcionModo.Completo, patientService.Object, new Mock<ICitaService>().Object, audit.Object, autoInitialize: false)
        { Nombre = "Paciente sintético" };
        var method = typeof(PanelRecepcionViewModel).GetMethod("AsegurarPacienteParaCita", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal(!failSave, (bool)method.Invoke(vm, null)!);
        patientService.Verify(s => s.Guardar(It.IsAny<Paciente>()), Times.Once);
        audit.Verify(a => a.RegistrarEvento(It.IsAny<AuditoriaEvento>()), Times.Never);
    }
}