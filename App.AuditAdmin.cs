using System.Windows;
using ClinicaLongevidadApp.Views;

namespace ClinicaLongevidadApp
{
    public partial class App : Application
    {
        public static void ShowAuditAdminWindow()
        {
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
