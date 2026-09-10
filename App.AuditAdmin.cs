using System.Windows;
using ClinicaLongevidadApp.Views;
using ClinicaLongevidadApp.Services;

namespace ClinicaLongevidadApp
{
    public partial class App : Application
    {
        public static void ShowAuditAdminWindow()
        {
            // Enforce admin-only access
            try
            {
                var role = Sesion.RolActual ?? string.Empty;
                var r = role.Trim().ToLowerInvariant();
                if (!(r.Contains("admin") || r.Contains("administr") || r.Contains("administrador")))
                {
                    MessageBox.Show("Acceso denegado. Solo usuarios con rol de administrador pueden abrir esta ventana.", "Acceso denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            catch
            {
                MessageBox.Show("Acceso denegado.", "Acceso denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? conn = null;
            try { conn = Current.Properties["AuditConnectionString"] as string; } catch { }
            if (string.IsNullOrWhiteSpace(conn))
            {
                MessageBox.Show("Audit connection string not available.", "Audit Admin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var w = new AuditAdminView(conn);
            w.Owner = Current.MainWindow;
            w.Show();
        }
    }
}
