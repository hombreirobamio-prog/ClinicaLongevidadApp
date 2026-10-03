using System;
using System.Collections.ObjectModel;
using System.Windows;
using ClinicaLongevidadApp.Commands;
using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.Views;

namespace ClinicaLongevidadApp.ViewModels
{
    public class UsuariosViewModel : BaseViewModel
    {
        public ObservableCollection<Usuario> Usuarios { get; }

        private Usuario? _usuarioSeleccionado;

        public Usuario? UsuarioSeleccionado
        {
            get => _usuarioSeleccionado;
            set => SetProperty(
                ref _usuarioSeleccionado,
                value);
        }

        public RelayCommand NuevoCommand { get; }
        public RelayCommand EditarCommand { get; }
        public RelayCommand EliminarCommand { get; }
        public RelayCommand VolverCommand { get; }

        public UsuariosViewModel()
        {
            Usuarios = new ObservableCollection<Usuario>(
                UsuarioService.ObtenerTodos());

            NuevoCommand = new RelayCommand(_ =>
            {
                App.DashboardViewModel!.CurrentView =
                    new UsuarioFormView
                    {
                        DataContext =
                            new UsuarioFormViewModel(
                                new Usuario())
                    };
            });

            EditarCommand = new RelayCommand(_ =>
            {
                if (UsuarioSeleccionado is null)
                {
                    MostrarAviso(
                        "Seleccione un usuario para editar.");
                    return;
                }

                App.DashboardViewModel!.CurrentView =
                    new UsuarioFormView
                    {
                        DataContext =
                            new UsuarioFormViewModel(
                                UsuarioSeleccionado)
                    };
            });

            EliminarCommand =
                new RelayCommand(_ => EliminarUsuario());

            VolverCommand = new RelayCommand(_ =>
            {
                App.DashboardViewModel!.CurrentView =
                    new HomeView();
            });
        }

        private void EliminarUsuario()
        {
            if (UsuarioSeleccionado is null)
            {
                MostrarAviso(
                    "Seleccione un usuario para eliminar.");
                return;
            }

            Usuario usuario = UsuarioSeleccionado;

            MessageBoxResult respuesta = MessageBox.Show(
                $"¿Desea eliminar al usuario " +
                $"'{usuario.NombreUsuario}'?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (respuesta != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                UsuarioService.Eliminar(usuario.Id);
                Usuarios.Remove(usuario);

                UsuarioSeleccionado = null;
            }
            catch (Exception ex)
            {
                RegistrarEventoUsuarioEliminado(usuario, false, ex.Message);

                MostrarAviso(
                    $"No se pudo eliminar el usuario: " +
                    $"{ex.Message}");
            }
        }

        private static void RegistrarEventoUsuarioEliminado(Usuario usuario, bool ok, string? error)
        {
            try
            {
                App.AuditoriaService?.RegistrarEvento(new AuditoriaEvento
                {
                    UsuarioAdmin = Sesion.UsuarioActual ?? "Sistema",
                    Accion = "Usuario.Eliminar",
                    Modulo = "Usuarios",
                    UsuarioAfectado = usuario.NombreUsuario,
                    Resultado = ok,
                    FechaHora = DateTime.Now,
                    Tipo = "Usuario",
                    Detalles = AuditoriaDetallesHelper.CrearJson(
                        ("UsuarioId", usuario.Id),
                        ("NombreUsuario", usuario.NombreUsuario),
                        ("Error", error ?? string.Empty))
                });
            }
            catch
            {
            }
        }

        private static void MostrarAviso(string mensaje)
        {
            MessageBox.Show(
                mensaje,
                "Gestión de usuarios",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}