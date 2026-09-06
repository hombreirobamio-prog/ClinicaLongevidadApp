using System;
using System.Collections.Generic;
using ClinicaLongevidadApp.Models;

namespace ClinicaLongevidadApp.Services
{
    public interface ICitaService
    {
        IEnumerable<Cita> ObtenerPorFecha(DateTime fecha);
        void Guardar(Cita cita);
        IEnumerable<Cita> ObtenerPorPaciente(int pacienteId);
        bool EstaOcupada(DateTime fecha, string hora, string profesional, int currentCitaId);
        //Cita? ObtenerPorId(int id);
    }
}
