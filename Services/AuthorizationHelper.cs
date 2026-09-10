using System;
using System.Linq;

namespace ClinicaLongevidadApp.Services
{
    // Simple, opt-in authorization helper for service-level checks.
    // Enforcement is disabled by default; enable with AUDIT_ENFORCE_AUTH=1.
    public static class AuthorizationHelper
    {
        public static bool EnforcementEnabled()
        {
            try
            {
                var v = Environment.GetEnvironmentVariable("AUDIT_ENFORCE_AUTH");
                Console.WriteLine($"[AuthorizationHelper] AUDIT_ENFORCE_AUTH='{v}'");
                return string.Equals(v, "1", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void EnsureRole(params string[] allowedRoles)
        {
            if (!EnforcementEnabled()) return;
            var current = Sesion.RolActual ?? string.Empty;
            try
            {
                Console.WriteLine($"[AuthorizationHelper] Enforcement enabled. CurrentRole='{current}' Allowed=[{string.Join(',', allowedRoles ?? new string[0])}]");
            }
            catch { }
            if (allowedRoles == null || allowedRoles.Length == 0) return;
            if (!allowedRoles.Any(r => string.Equals(r ?? string.Empty, current, StringComparison.OrdinalIgnoreCase)))
            {
                throw new UnauthorizedAccessException("Operación no autorizada para el rol actual.");
            }
        }

        public static void EnsureAdminOrSelf(string usuarioAfectado)
        {
            if (!EnforcementEnabled()) return;
            var currentUser = Sesion.UsuarioActual ?? string.Empty;
            var currentRole = Sesion.RolActual ?? string.Empty;
            if (string.Equals(currentUser, usuarioAfectado, StringComparison.OrdinalIgnoreCase)) return;
            if (string.Equals(currentRole, "Administración", StringComparison.OrdinalIgnoreCase)) return;
            throw new UnauthorizedAccessException("Operación no autorizada: solo el propio usuario o Administración pueden realizar esta acción.");
        }
    }
}
