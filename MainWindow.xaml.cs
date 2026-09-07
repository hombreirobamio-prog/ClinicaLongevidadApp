using System.Windows;

using System;
using System.Windows;
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
            }
            catch { }
        }

        private void MenuItemAuditAdmin_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            App.ShowAuditAdminWindow();
        }

        private void MenuItemRecentAudit_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            string? conn = null;
            try { conn = App.Current.Properties["AuditConnectionString"] as string; } catch { }
            if (string.IsNullOrWhiteSpace(conn))
            {
                MessageBox.Show("Audit connection string not available.", "Audit Recent", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var w = new Views.RecentAuditWindow(conn);
            w.Owner = this;
            w.Show();
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
