using System;
using SQLite;

namespace ClinicaLongevidadApp.Models
{
    public class Cita
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int PacienteId { get; set; }
        public string PacienteNombre { get; set; } = "";
        public DateTime Fecha { get; set; } = DateTime.Today;
        public string Hora { get; set; } = "";
        public string Profesional { get; set; } = "";
        public string Estado { get; set; } = "Pendiente";
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}
