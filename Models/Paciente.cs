using System;
using SQLite;

namespace ClinicaLongevidadApp.Models
{
    public class Paciente
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string NombreCompleto { get; set; } = "";
        public string DNI { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime? FechaNacimiento { get; set; }
        public string Sexo { get; set; } = "";
        public string Calle { get; set; } = "";
        public string Numero { get; set; } = "";
        public string Piso { get; set; } = "";
        public string CP { get; set; } = "";
        public string Municipio { get; set; } = "";
        public string Provincia { get; set; } = "";
        public string ProteccionDatos { get; set; } = "";
        public string Firma { get; set; } = "";
        public DateTime? FechaAlta { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}
