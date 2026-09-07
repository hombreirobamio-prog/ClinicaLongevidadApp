using System.Windows;

using System;
using System.Windows;

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
            Services.Sesion.SessionChanged += () => UpdateAdminMenuVisibility();
        }

        private void UpdateAdminMenuVisibility()
        {
            try
            {
                var isAdmin = false;
                var role = Services.Sesion.RolActual;
                if (!string.IsNullOrWhiteSpace(role))
                {
                    isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
                }
                MenuItemAuditAdmin.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        private void MenuItemAuditAdmin_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            App.ShowAuditAdminWindow();
        }
    }
}
