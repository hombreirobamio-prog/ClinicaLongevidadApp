using System.Windows;
using System.Windows.Controls;
using ClinicaLongevidadApp.ViewModels;

namespace ClinicaLongevidadApp.Views
{
    public partial class UsuarioFormView : UserControl
    {
        public UsuarioFormView()
        {
            InitializeComponent();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is UsuarioFormViewModel vm && sender is PasswordBox pb)
            {
                // Guardamos la contraseña temporalmente en el ViewModel.
                // UsuarioService se encargará de aplicar PBKDF2 al guardar.
                vm.Usuario.PasswordHash = pb.Password;
            }
        }
    }
}
