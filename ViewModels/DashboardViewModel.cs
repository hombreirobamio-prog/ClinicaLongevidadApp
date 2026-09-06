using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using ClinicaLongevidadApp.Commands;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.Views;

namespace ClinicaLongevidadApp.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        private UserControl? _currentView;

        public UserControl? CurrentView
        {
            get => _currentView;
            set
            {
                _currentView = value;
                OnPropertyChanged();
            }
        }

        public bool EsAdministracion => EsSesionAdministracion();

        public bool PuedeVerUsuarios => EsAdministracion;

        public bool PuedeVerAuditoria => EsAdministracion;

        public RelayCommand IrHomeCommand { get; }

        public RelayCommand IrUsuariosCommand { get; }

        public RelayCommand MostrarAuditoriaCommand { get; }

        public RelayCommand MostrarFestivosCommand { get; }

        public RelayCommand CerrarSesionCommand { get; }

        public DashboardViewModel()
        {
            CurrentView = new HomeView();

            IrHomeCommand = new RelayCommand(_ =>
            {
                if (!PuedeAccederAdministracion())
                {
                    return;
                }

                CurrentView = new HomeView();
            });

            IrUsuariosCommand = new RelayCommand(_ =>
            {
                if (!PuedeAccederAdministracion())
                {
                    return;
                }

                CurrentView = new UsuariosView();
            });

            MostrarAuditoriaCommand = new RelayCommand(_ =>
            {
                if (!PuedeAccederAdministracion())
                {
                    return;
                }

                if (App.AuditoriaService is null)
                {
                    return;
                }

                var vista = new AuditoriaView
                {
                    DataContext = new AuditoriaViewModel(
                        App.AuditoriaService)
                };

                CurrentView = vista;
            });

            MostrarFestivosCommand = new RelayCommand(_ =>
            {
                if (!PuedeAccederAdministracion())
                {
                    return;
                }

                CurrentView = new FestivosView();
            });

            CerrarSesionCommand = new RelayCommand(_ => App.CerrarSesion());
        }

        private static bool EsSesionAdministracion()
        {
            return string.Equals(
                       Sesion.RolActual,
                       "Administración",
                       StringComparison.OrdinalIgnoreCase)
                   ||
                   string.Equals(
                       Sesion.AreaActual,
                       "Administración",
                       StringComparison.OrdinalIgnoreCase);
        }

        private bool PuedeAccederAdministracion()
        {
            if (EsAdministracion)
            {
                return true;
            }

            MessageBox.Show(
                "No tiene permisos para acceder a esta opción.",
                "Acceso no autorizado",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(
            [CallerMemberName] string? nombrePropiedad = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nombrePropiedad));
        }
    }
}