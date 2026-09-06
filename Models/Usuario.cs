using System;
using SQLite;

namespace ClinicaLongevidadApp.Models
{
    public class Usuario
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string NombreUsuario { get; set; } = "";
        public string NombreCompleto { get; set; } = "";
        public string Email { get; set; } = "";
        public string TelefonoInterno { get; set; } = "";

        public string PasswordHash { get; set; } = "";

        public bool Activo { get; set; } = true;

        public string Rol { get; set; } = "";
        public string Area { get; set; } = "";

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime? UltimoAcceso { get; set; }
    }
}
