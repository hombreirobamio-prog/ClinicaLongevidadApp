using System;

namespace ClinicaLongevidadApp.Models
{
    public class AuditoriaModel
    {
        public int Id { get; set; }
        public string EventId { get; set; } = "";

        public string UsuarioAdmin { get; set; } = "";
        public string Accion { get; set; } = "";
        public DateTime FechaHora { get; set; }
        public string Modulo { get; set; } = "";
        public string UsuarioAfectado { get; set; } = "";
        public string Resultado { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Detalles { get; set; } = "";

        public string Rol { get; set; } = "";
        public string Area { get; set; } = "";
        public string SesionId { get; set; } = "";
        public string Equipo { get; set; } = "";
        public string VersionApp { get; set; } = "";
        public string KeyVersion { get; set; } = "";
        public string KeyVersionEnc { get; set; } = "";
    }
}
