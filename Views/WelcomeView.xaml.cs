using System.Windows;
using System.Windows.Controls;
using ClinicaLongevidadApp.ViewModels;

namespace ClinicaLongevidadApp.Views
{
    public partial class WelcomeView : UserControl
    {
        public WelcomeView()
        {
            InitializeComponent();
            DataContext = new WelcomeViewModel();
        }

        private void BtnAcceder_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is WelcomeViewModel vm)
            {
                string areaSeleccionada = vm.AreaSeleccionada ?? "";

                if (string.IsNullOrWhiteSpace(areaSeleccionada))
                {
                    MessageBox.Show(
                        "Debe seleccionar un área antes de continuar.",
                        "Aviso",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }

                // ⭐ Crear LoginView correctamente con el área seleccionada
                var loginView = new LoginView(areaSeleccionada);

                // ⭐ Navegar al LoginView SIN NavigationService
                Application.Current.MainWindow.Content = loginView;
            }
        }
    }
}
