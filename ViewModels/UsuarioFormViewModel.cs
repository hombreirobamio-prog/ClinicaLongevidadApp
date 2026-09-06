using System;
using System.Collections.ObjectModel;
using System.Windows;
using ClinicaLongevidadApp.Commands;
using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.Views;

namespace ClinicaLongevidadApp.ViewModels
{
    public class UsuarioFormViewModel : BaseViewModel
    {
        public Usuario Usuario { get; }

        private string? _mensajeError;
        private readonly string _rolOriginal;
        private readonly string _areaOriginal;
        private readonly bool _activoOriginal;
        private readonly bool _esUsuarioNuevo;

        public string? MensajeError
        {
            get => _mensajeError;
            set => SetProperty(ref _mensajeError, value);
        }

        public ObservableCollection<string> Roles { get; } =
        [
            "Administración",
            "Recepción",
            "Médico",
            "Enfermería",
            "Dirección",
            "Sistemas"
        ];

        public ObservableCollection<string> Areas { get; } =
        [
            "Medicina General",
            "Recepción",
            "Enfermería",
            "Laboratorio",
            "Administración",
            "Gerencia",
            "Sistemas"
        ];

        public RelayCommand GuardarCommand { get; }
        public RelayCommand VolverCommand { get; }
        public RelayCommand RestablecerPasswordCommand { get; }

        public UsuarioFormViewModel(Usuario usuario)
        {
            ArgumentNullException.ThrowIfNull(usuario);

            Usuario = usuario;
            MensajeError = string.Empty;

            _esUsuarioNuevo = Usuario.Id == 0;
            _rolOriginal = Usuario.Rol;
            _areaOriginal = Usuario.Area;
            _activoOriginal = Usuario.Activo;

            GuardarCommand = new RelayCommand(_ => Guardar());

            VolverCommand = new RelayCommand(_ => Volver());

            RestablecerPasswordCommand = new RelayCommand(_ =>
            {
                if (Usuario.Id <= 0)
                {
                    MensajeError =
                        "Primero debe guardar el usuario antes de restablecer la contraseña.";
                    return;
                }

                RestablecerPassword();
            });
        }

        private void Guardar()
        {
            MensajeError = string.Empty;

            if (string.IsNullOrWhiteSpace(Usuario.NombreUsuario))
            {
                MensajeError =
                    "El nombre de usuario es obligatorio.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Usuario.NombreCompleto))
            {
                MensajeError =
                    "El nombre completo es obligatorio.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Usuario.Rol))
            {
                MensajeError =
                    "Debe seleccionar un rol.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Usuario.Area))
            {
                MensajeError =
                    "Debe seleccionar un área.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(Usuario.Email) &&
                !Usuario.Email.Contains('@'))
            {
                MensajeError =
                    "El email no es válido.";
                return;
            }

            if (_esUsuarioNuevo &&
                string.IsNullOrWhiteSpace(Usuario.PasswordHash))
            {
                MensajeError =
                    "Debe introducir una contraseña.";
                return;
            }

            var existente = UsuarioService.ObtenerPorNombre(
                Usuario.NombreUsuario);

            if (existente is not null &&
                existente.Id != Usuario.Id)
            {
                MensajeError =
                    "Ya existe un usuario con ese nombre.";
                return;
            }

            try
            {
                UsuarioService.Guardar(Usuario);

                RegistrarCambiosDeUsuario();

                App.DashboardViewModel!.CurrentView =
                    new UsuariosView
                    {
                        DataContext = new UsuariosViewModel()
                    };
            }
            catch (Exception ex)
            {
                RegistrarEventoUsuario(
                    _esUsuarioNuevo ? "Usuario.Crear" : "Usuario.Editar",
                    false,
                    Usuario.NombreUsuario,
                    AuditoriaDetallesHelper.CrearJson(
                        ("UsuarioId", Usuario.Id),
                        ("Error", ex.Message)));

                MensajeError =
                    $"No se pudo guardar el usuario: {ex.Message}";
            }
        }

        private void RegistrarCambiosDeUsuario()
        {
            string usuarioAdmin =
                Sesion.UsuarioActual ?? "Sistema";

            if (_esUsuarioNuevo)
            {
                RegistrarEventoUsuario(
                    "Usuario.Crear",
                    true,
                    Usuario.NombreUsuario,
                    AuditoriaDetallesHelper.CrearJson(
                        ("UsuarioId", Usuario.Id),
                        ("Rol", Usuario.Rol),
                        ("Area", Usuario.Area)));

                return;
            }

            bool seHaRegistradoUnCambio = false;

            bool cambioPassword =
                !string.IsNullOrWhiteSpace(Usuario.PasswordHash) &&
                !Usuario.PasswordHash.StartsWith(
                    "PBKDF2$",
                    StringComparison.Ordinal);

            if (cambioPassword)
            {
                RegistrarEventoUsuario(
                    "Usuario.CambiarPassword",
                    true,
                    Usuario.NombreUsuario,
                    AuditoriaDetallesHelper.CrearJson(
                        ("UsuarioId", Usuario.Id)));

                seHaRegistradoUnCambio = true;
            }

            if (!string.Equals(
                    _rolOriginal,
                    Usuario.Rol,
                    StringComparison.Ordinal))
            {
                RegistrarEventoUsuario(
                    "Usuario.CambiarRol",
                    true,
                    Usuario.NombreUsuario,
                    AuditoriaDetallesHelper.CrearJson(
                        ("UsuarioId", Usuario.Id),
                        ("RolAnterior", _rolOriginal),
                        ("RolNuevo", Usuario.Rol)));

                seHaRegistradoUnCambio = true;
            }

            if (!string.Equals(
                    _areaOriginal,
                    Usuario.Area,
                    StringComparison.Ordinal))
            {
                RegistrarEventoUsuario(
                    "Usuario.CambiarArea",
                    true,
                    Usuario.NombreUsuario,
                    AuditoriaDetallesHelper.CrearJson(
                        ("UsuarioId", Usuario.Id),
                        ("AreaAnterior", _areaOriginal),
                        ("AreaNueva", Usuario.Area)));

                seHaRegistradoUnCambio = true;
            }

            if (_activoOriginal != Usuario.Activo)
            {
                RegistrarEventoUsuario(
                    Usuario.Activo
                        ? "Usuario.Activar"
                        : "Usuario.Desactivar",
                    true,
                    Usuario.NombreUsuario,
                    AuditoriaDetallesHelper.CrearJson(
                        ("UsuarioId", Usuario.Id),
                        ("Activo", Usuario.Activo)));

                seHaRegistradoUnCambio = true;
            }

            if (!seHaRegistradoUnCambio)
            {
                RegistrarEventoUsuario(
                    "Usuario.Editar",
                    true,
                    Usuario.NombreUsuario,
                    AuditoriaDetallesHelper.CrearJson(
                        ("UsuarioId", Usuario.Id)));
            }
        }

        private void RestablecerPassword()
        {
            if (Usuario.Id <= 0)
            {
                MensajeError =
                    "No se puede restablecer la contraseña de un usuario nuevo.";
                return;
            }

            try
            {
                string nuevaPassword =
                    UsuarioService.RestablecerContraseña(
                        Usuario.Id);

                MessageBox.Show(
                    $"Nueva contraseña temporal:\n\n" +
                    $"{nuevaPassword}\n\n" +
                    "El usuario deberá cambiarla al iniciar sesión.",
                    "Contraseña restablecida",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                RegistrarEventoUsuario(
                    "Usuario.RestablecerPassword",
                    true,
                    Usuario.NombreUsuario,
                    AuditoriaDetallesHelper.CrearJson(
                        ("UsuarioId", Usuario.Id)));
            }
            catch (Exception ex)
            {
                RegistrarEventoUsuario(
                    "Usuario.RestablecerPassword",
                    false,
                    Usuario.NombreUsuario,
                    AuditoriaDetallesHelper.CrearJson(
                        ("UsuarioId", Usuario.Id),
                        ("Error", ex.Message)));

                MensajeError =
                    $"Error al restablecer contraseña: {ex.Message}";
            }
        }

        private static void RegistrarEventoUsuario(string accion, bool ok, string usuarioAfectado, string detalles)
        {
            try
            {
                App.AuditoriaService?.RegistrarEvento(new AuditoriaEvento
                {
                    UsuarioAdmin = Sesion.UsuarioActual ?? "Sistema",
                    Accion = accion,
                    Modulo = "Usuarios",
                    UsuarioAfectado = usuarioAfectado ?? string.Empty,
                    Resultado = ok,
                    FechaHora = DateTime.Now,
                    Tipo = "Usuario",
                    Detalles = detalles ?? string.Empty
                });
            }
            catch
            {
            }
        }

        private static void Volver()
        {
            App.DashboardViewModel!.CurrentView =
                new UsuariosView
                {
                    DataContext = new UsuariosViewModel()
                };
        }
    }
}