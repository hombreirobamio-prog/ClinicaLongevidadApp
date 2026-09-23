using System.Windows;

using System;
using ClinicaLongevidadApp.Services;

namespace ClinicaLongevidadApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (App.DashboardViewModel is not null)
            {
                DataContext = App.DashboardViewModel;
            }

            // Update admin menu visibility based on session
            UpdateAdminMenuVisibility();
            Sesion.SessionChanged += () => UpdateAdminMenuVisibility();
#if DEBUG
            try { MenuItemDebugAdmin.Visibility = Visibility.Visible; } catch { }
#endif
        }

        private void UpdateAdminMenuVisibility()
        {
            try
            {
                var isAdmin = false;
                var role = Sesion.RolActual;
                if (!string.IsNullOrWhiteSpace(role))
                {
                    var r = role.Trim().ToLowerInvariant();
                    // Accept common admin role names in Spanish/English
                    if (r.Contains("admin") || r.Contains("administr") || r.Contains("administrador"))
                    {
                        isAdmin = true;
                    }
                }
                MenuItemAuditAdmin.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
                MenuItemRecentAudit.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
                try
                {
                    var obj = this.FindName("MenuItemAuditDiagnostics") as System.Windows.FrameworkElement;
                    if (obj != null) obj.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
                }
                catch { }
            }
            catch { }
        }

        private async void MenuItemAuditDiagnostics_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                string? conn = null;
                try { conn = Application.Current.Properties["AuditConnectionString"] as string; } catch { }
                conn ??= Environment.GetEnvironmentVariable("AUDIT_DB") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(conn))
                {
                    Services.DialogHelper.ShowWarning("Audit Diagnostics", "Audit connection string not available.");
                    return;
                }

                var svc = new Services.AuditoriaService(conn);

                // Run diagnostics in background to avoid UI freeze
                var quick = await System.Threading.Tasks.Task.Run(() => svc.GenerateQuickDiagnostics());
                var report = await System.Threading.Tasks.Task.Run(() => svc.GenerateIntegrityDiagnosticReport());

                var msg = new System.Text.StringBuilder();
                msg.AppendLine("Diagnóstico completado.");
                if (!string.IsNullOrWhiteSpace(quick)) msg.AppendLine("Ficheros rápidos escritos en: " + quick);
                if (!string.IsNullOrWhiteSpace(report)) msg.AppendLine("Informe de integridad escrito en: " + report);
                Services.DialogHelper.ShowInfo("Audit Diagnostics", msg.ToString());
            }
            catch (Exception ex)
            {
                try { Services.DialogHelper.ShowError("Audit Diagnostics", "Error generando diagnósticos: " + ex.Message); } catch { }
            }
        }

        private void MenuItemAuditAdmin_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            App.ShowAuditAdminWindow();
        }

        private async void MenuItemRecentAudit_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                // Try to find the AuditoriaView in the visual tree and ask its ViewModel to load last 10 into the panel
                var auditoriaView = FindVisualChild<Views.AuditoriaView>(this);
                if (auditoriaView is not null && auditoriaView.DataContext is ViewModels.AuditoriaViewModelV2 vm2)
                {
                    try
                    {
                        await vm2.CargarUltimosAsync(10);
                        return;
                    }
                    catch { /* fall through to opening auxiliary window */ }
                }
            }
            catch { }

            // Fallback: open RecentAuditWindow
            string? conn = null;
            try { conn = App.Current.Properties["AuditConnectionString"] as string; } catch { }
            if (string.IsNullOrWhiteSpace(conn))
            {
                Services.DialogHelper.ShowWarning("Audit Recent", "Audit connection string not available.");
                return;
            }

            var w = new Views.RecentAuditWindow(conn);
            w.Owner = this;
            w.Show();
        }

        private static T? FindVisualChild<T>(DependencyObject? elemento) where T : DependencyObject
        {
            if (elemento is null)
            {
                return null;
            }

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(elemento); i++)
            {
                var hijo = System.Windows.Media.VisualTreeHelper.GetChild(elemento, i);

                if (hijo is T resultado)
                {
                    return resultado;
                }

                var resultadoHijo = FindVisualChild<T>(hijo);

                if (resultadoHijo is not null)
                {
                    return resultadoHijo;
                }
            }

            return null;
        }

        private void MenuItemDebugAdmin_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                // Toggle admin role for development/testing
                if (string.Equals(Sesion.RolActual, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    Sesion.RolActual = null;
                }
                else
                {
                    Sesion.RolActual = "Admin";
                }
                Sesion.NotifyChanged();
            }
            catch { }
        }
    }
}
