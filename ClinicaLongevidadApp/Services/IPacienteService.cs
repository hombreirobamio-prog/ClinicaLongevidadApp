using System.Collections.Generic;
using ClinicaLongevidadApp.Models;

namespace ClinicaLongevidadApp.Services
{
    public interface IPacienteService
    {
        IEnumerable<Paciente> ObtenerTodos();
        Paciente? ObtenerPorId(int id);
        void Guardar(Paciente paciente);
    }
}
