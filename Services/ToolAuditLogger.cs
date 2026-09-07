using ClinicaLongevidadApp.Models;
using System;
namespace ClinicaLongevidadApp.Services
{
    public static class ToolAuditLogger
    {
        /// <summary>
        /// Best-effort helper to log an operation performed by operator tools.
        /// Does not throw; failures are swallowed to avoid impacting tooling flow.
        /// </summary>
        public static void RegistrarOperacion(string connectionString, string accion, bool resultado, object detalles, string modulo = "RotateKeys", string? usuario = null)
        {
            try
            {
                var svc = new AuditoriaService(connectionString);
                var evento = new AuditoriaEvento
                {
                    Accion = accion,
                    Modulo = modulo,
                    UsuarioAdmin = usuario ?? Environment.UserName ?? string.Empty,
                    Resultado = resultado,
                    Detalles = System.Text.Json.JsonSerializer.Serialize(detalles ?? new { }),
                    Tipo = "Operación"
                };
                svc.RegistrarEvento(evento);
            }
            catch
            {
                // best-effort: swallow any error
            }
        }
    }
}
