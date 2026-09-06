using System.Windows;
using System.Windows.Controls;
using ClinicaLongevidadApp.ViewModels;

namespace ClinicaLongevidadApp.Views
{
    public partial class LoginView : UserControl
    {
        public LoginView(string areaSeleccionada)
        {
            InitializeComponent();
            DataContext = new LoginViewModel(areaSeleccionada, App.AuditoriaService!);
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.PasswordHash = ((PasswordBox)sender).Password;
            }
        }
    }
}
