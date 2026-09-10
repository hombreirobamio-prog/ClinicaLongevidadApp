using System.Windows;
using ClinicaLongevidadApp.Services;

namespace ClinicaLongevidadApp.Views
{
    public partial class RecentAuditWindow : Window
    {
        private readonly AuditAdminService _svc;
        public RecentAuditWindow(string connectionString)
        {
            InitializeComponent();
            _svc = new AuditAdminService(connectionString);
            Loaded += RecentAuditWindow_Loaded;
        }

        private void RecentAuditWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            try
            {
                // Request all recent audits (no limit) so admin window shows full history
                var rows = _svc.GetRecentAudits(0);
                Grid.ItemsSource = rows;
            }
            catch { }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshGrid();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
