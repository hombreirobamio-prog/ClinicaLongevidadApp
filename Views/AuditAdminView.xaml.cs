using System.Windows;
using ClinicaLongevidadApp.ViewModels;

namespace ClinicaLongevidadApp.Views
{
    public partial class AuditAdminView : Window
    {
        private readonly AuditAdminViewModel _vm;

        public AuditAdminView(string connectionString)
        {
            InitializeComponent();
            _vm = new AuditAdminViewModel(connectionString);
            DataContext = _vm;
            Loaded += AuditAdminView_Loaded;
        }

        private async void AuditAdminView_Loaded(object sender, RoutedEventArgs e)
        {
            await _vm.RefreshAsync();
        }
    }
}
