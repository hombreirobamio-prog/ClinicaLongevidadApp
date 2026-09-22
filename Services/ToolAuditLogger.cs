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
                    Tipo = "Operación",
                    // Provide explicit metadata: prefer current session values when available
                    Rol = Services.Sesion.RolActual ?? string.Empty,
                    Area = Services.Sesion.AreaActual ?? string.Empty,
                    SesionId = Services.Sesion.UsuarioActual ?? Guid.NewGuid().ToString("N"),
                    Equipo = Environment.MachineName ?? string.Empty,
                    VersionApp = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty
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
