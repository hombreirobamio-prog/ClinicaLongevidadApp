using System;

namespace ClinicaLongevidadApp.Models
{
    public class AuditoriaEvento
    {
        // Identificador único del evento
        public string EventId { get; init; } = Guid.NewGuid().ToString("N");

        // Hash encadenado del evento (se calcula en el servicio)
        public string? PrevHash { get; init; }
        public string? Hash { get; init; }

        // Firma HMAC del evento
        public string? Signature { get; init; }

        public string UsuarioAdmin { get; init; } = "";
        public string Accion { get; init; } = "";
        public string Modulo { get; init; } = "";
        public string UsuarioAfectado { get; init; } = "";
        public bool Resultado { get; init; }
        public DateTime FechaHora { get; init; } = DateTime.UtcNow;
        public string? Detalles { get; init; }
        public string? Tipo { get; init; }

        public string? Rol { get; init; }
        public string? Area { get; init; }
        public string? SesionId { get; init; }
        public string? Equipo { get; init; }
        public string? VersionApp { get; init; }
    }
}
