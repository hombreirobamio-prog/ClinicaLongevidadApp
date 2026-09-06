using System.Windows;
using ClinicaLongevidadApp.Commands;
using ClinicaLongevidadApp.Views;

namespace ClinicaLongevidadApp.ViewModels
{
    public class WelcomeViewModel : BaseViewModel
    {
        private string? _areaSeleccionada;
        public string? AreaSeleccionada
        {
            get => _areaSeleccionada;
            set => SetProperty(ref _areaSeleccionada, value);
        }

        public RelayCommand AccederCommand { get; }

        public WelcomeViewModel()
        {
            AccederCommand = new RelayCommand(_ =>
            {
                // ⭐ Navegar a la selección de área SIN NavigationService
                Application.Current.MainWindow.Content = new AreaSelectionView();
            });
        }
    }
}
