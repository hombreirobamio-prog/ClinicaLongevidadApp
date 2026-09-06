namespace ClinicaLongevidadApp.Services
{
    public static class Sesion
    {
        // ⭐ Usuario actual de la sesión (puede ser null)
        public static string? UsuarioActual { get; set; }

        public static string? RolActual { get; set; }

        public static string? AreaActual { get; set; }

        // Evento para notificar cambios en la sesión (login/logout/role change)
        public static event System.Action? SessionChanged;

        public static void NotifyChanged()
        {
            try
            {
                SessionChanged?.Invoke();
            }
            catch
            {
                // proteger contra excepciones en handlers
            }
        }

        public static void Limpiar()
        {
            UsuarioActual = null;
            RolActual = null;
            AreaActual = null;
            NotifyChanged();
        }
    }
}
