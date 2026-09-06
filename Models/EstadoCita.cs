namespace ClinicaLongevidadApp.Models
{
    /// <summary>
    /// Estados posibles de una cita en el sistema.
    /// </summary>
    public enum EstadoCita
    {
        /// <summary>Cita pendiente de confirmación</summary>
        Pendiente,

        /// <summary>Cita confirmada por el paciente</summary>
        Confirmada,

        /// <summary>Paciente en sala de espera</summary>
        SalaEspera,

        /// <summary>Cita en consulta</summary>
        EnConsulta,

        /// <summary>Cita finalizada</summary>
        Finalizada,

        /// <summary>Cita cancelada</summary>
        Cancelada,

        /// <summary>Cita facturada</summary>
        Facturada
    }

    /// <summary>
    /// Extensiones para trabajar con estados de cita.
    /// </summary>
    public static class EstadoCitaExtensions
    {
        /// <summary>
        /// Obtiene la representación en texto del estado de cita.
        /// </summary>
        public static string GetDisplayName(this EstadoCita estado)
        {
            return estado switch
            {
                EstadoCita.Pendiente => "Pendiente",
                EstadoCita.Confirmada => "Confirmada",
                EstadoCita.SalaEspera => "Sala espera",
                EstadoCita.EnConsulta => "En consulta",
                EstadoCita.Finalizada => "Finalizada",
                EstadoCita.Cancelada => "Cancelada",
                EstadoCita.Facturada => "Facturada",
                _ => "Desconocido"
            };
        }

        /// <summary>
        /// Convierte una cadena de texto a EstadoCita.
        /// </summary>
        public static EstadoCita? ParseEstado(string? estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
                return null;

            return estado.Trim().ToLowerInvariant() switch
            {
                "pendiente" => EstadoCita.Pendiente,
                "confirmada" or "confirmado" => EstadoCita.Confirmada,
                "sala espera" or "sala de espera" => EstadoCita.SalaEspera,
                "en consulta" => EstadoCita.EnConsulta,
                "finalizada" or "finalizado" or "terminada" or "terminado" => EstadoCita.Finalizada,
                "cancelada" => EstadoCita.Cancelada,
                "facturada" => EstadoCita.Facturada,
                _ => null
            };
        }
    }
}
