using System.Collections.Generic;
using ClinicaLongevidadApp.Models;

namespace ClinicaLongevidadApp.Services
{
    public class PacienteServiceAdapter : IPacienteService
    {
        public IEnumerable<Paciente> ObtenerTodos() => PacienteService.ObtenerTodos();

        public Paciente? ObtenerPorId(int id) => PacienteService.ObtenerPorId(id);

        public void Guardar(Paciente paciente) => PacienteService.Guardar(paciente);
    }
}
