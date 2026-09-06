using System;
using System.Collections.Generic;
using ClinicaLongevidadApp.Models;

namespace ClinicaLongevidadApp.Services
{
    public class CitaServiceAdapter : ICitaService
    {
        public IEnumerable<Cita> ObtenerPorFecha(DateTime fecha) => CitaService.ObtenerPorFecha(fecha);
        public void Guardar(Cita cita) => CitaService.Guardar(cita);
        public IEnumerable<Cita> ObtenerPorPaciente(int pacienteId) => CitaService.ObtenerPorPaciente(pacienteId);
        public bool EstaOcupada(DateTime fecha, string hora, string profesional, int currentCitaId) => CitaService.EstaOcupada(fecha, hora, profesional, currentCitaId);
        // ObtenerPorId not available on static CitaService
    }
}
