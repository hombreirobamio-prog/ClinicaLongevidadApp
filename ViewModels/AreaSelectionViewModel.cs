using System.Windows;
using ClinicaLongevidadApp.Commands;
using ClinicaLongevidadApp.Views;

namespace ClinicaLongevidadApp.ViewModels
{
    public class AreaSelectionViewModel
    {
        public RelayCommand AdministracionCommand { get; }
        public RelayCommand RecepcionCommand { get; }
        public RelayCommand MedicoCommand { get; }

        public AreaSelectionViewModel()
        {
            // ⭐ ADMINISTRACIÓN
            AdministracionCommand = new RelayCommand(_ =>
            {
                var login = new LoginView("Administración");
                Application.Current.MainWindow.Content = login;
            });

            // ⭐ RECEPCIÓN
            RecepcionCommand = new RelayCommand(_ =>
            {
                var login = new LoginView("Recepción");
                Application.Current.MainWindow.Content = login;
            });

            // ⭐ MÉDICO
            MedicoCommand = new RelayCommand(_ =>
            {
                var login = new LoginView("Médico");
                Application.Current.MainWindow.Content = login;
            });
        }
    }
}
