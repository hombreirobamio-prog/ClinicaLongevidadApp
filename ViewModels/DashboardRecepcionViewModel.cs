using ClinicaLongevidadApp.Core;
using ClinicaLongevidadApp.Views;
using System.Windows.Input;

namespace ClinicaLongevidadApp.ViewModels
{
    public class DashboardRecepcionViewModel : ViewModelBase
    {
        private object? _vistaActual;
        public object? VistaActual
        {
            get => _vistaActual;
            set { _vistaActual = value; OnPropertyChanged(); }
        }

        public ICommand MostrarPanelRecepcionCommand { get; }
        public ICommand MostrarCitasCommand { get; }
        public ICommand MostrarPacientesCommand { get; }
        public ICommand CerrarSesionCommand { get; }

        public DashboardRecepcionViewModel()
        {
            MostrarPanelRecepcionCommand = new RelayCommand(_ =>
            {
                VistaActual = new PanelRecepcionView(new PanelRecepcionViewModel(PanelRecepcionModo.Completo));
            });

            MostrarCitasCommand = new RelayCommand(_ =>
            {
                VistaActual = new PanelRecepcionView(new PanelRecepcionViewModel(PanelRecepcionModo.Citas));
            });

            MostrarPacientesCommand = new RelayCommand(_ =>
            {
                VistaActual = new PanelRecepcionView(new PanelRecepcionViewModel(PanelRecepcionModo.Pacientes));
            });

            CerrarSesionCommand = new RelayCommand(_ => App.CerrarSesion());

            // Vista inicial
            VistaActual = new PanelRecepcionView(new PanelRecepcionViewModel(PanelRecepcionModo.Completo));
        }
    }
}
