using System;
using System.Collections.Generic;
using System.Windows;
using ClinicaLongevidadApp.Commands;
using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.Views;

namespace ClinicaLongevidadApp.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly AuditoriaService _auditoria;
        private static readonly object LoginAttemptsLock = new();
        private static readonly Dictionary<string, LoginAttemptState> LoginAttempts = new();
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LoginLockoutDuration = TimeSpan.FromMinutes(1);
        private bool _isLoggingIn;
        private string? _area;

        private sealed class LoginAttemptState
        {
            public int FailedAttempts { get; set; }
            public DateTime? LockedUntilUtc { get; set; }
        }

        public string? Area
        {
            get => _area;
            set => SetProperty(ref _area, value);
        }

        private string? _nombreUsuario;

        public string? NombreUsuario
        {
            get => _nombreUsuario;
            set => SetProperty(ref _nombreUsuario, value);
        }

        public string PasswordHash { get; set; } = string.Empty;

        public RelayCommand EntrarCommand { get; }

        public LoginViewModel(
            string areaSeleccionada,
            AuditoriaService auditoriaService)
        {
            Area = areaSeleccionada;
            NombreUsuario = string.Empty;
            _auditoria = auditoriaService;

            EntrarCommand = new RelayCommand(_ => Entrar());
        }

        private void Entrar()
        {
            if (_isLoggingIn)
            {
                return;
            }

            _isLoggingIn = true;

            try
            {
                string nombreUsuario = NombreUsuario?.Trim() ?? string.Empty;
                string areaSeleccionada = Area?.Trim() ?? string.Empty;

                if (EstaBloqueado(nombreUsuario, out TimeSpan tiempoRestante))
                {
                    MostrarMensaje(
                        $"Demasiados intentos. Espere {Math.Ceiling(tiempoRestante.TotalSeconds)} segundos.",
                        "Inicio de sesión",
                        MessageBoxImage.Warning);
                    return;
                }

                var usuario = UsuarioService.ObtenerPorNombre(nombreUsuario);

                if (usuario is null ||
                    !UsuarioService.ValidarLogin(
                        nombreUsuario,
                        PasswordHash))
                {
                    RegistrarEventoLogin(
                        accion: "Login.FallidoCredenciales",
                        usuarioAdmin: usuario?.NombreUsuario ?? "Desconocido",
                        usuarioAfectado: nombreUsuario,
                        ok: false,
                        detalles: AuditoriaDetallesHelper.CrearJson(
                            ("Area", areaSeleccionada),
                            ("UsuarioIntentado", nombreUsuario)));

                    RegistrarIntentoFallido(nombreUsuario);

                    MostrarMensaje(
                        "El nombre de usuario o la contraseña no son correctos.",
                        "Inicio de sesión",
                        MessageBoxImage.Warning);

                    return;
                }

                if (!TienePermisoParaArea(usuario, areaSeleccionada))
                {
                    RegistrarEventoLogin(
                        accion: "Login.AccesoNoAutorizadoArea",
                        usuarioAdmin: usuario.NombreUsuario,
                        usuarioAfectado: areaSeleccionada,
                        ok: false,
                        detalles: AuditoriaDetallesHelper.CrearJson(
                            ("Area", areaSeleccionada),
                            ("Usuario", usuario.NombreUsuario)));

                    MostrarMensaje(
                        $"El usuario no tiene permiso para acceder al área de {areaSeleccionada}.",
                        "Acceso no autorizado",
                        MessageBoxImage.Error);

                    return;
                }

                // Set session first so the audit event records the role and area.
                Sesion.UsuarioActual = usuario.NombreUsuario;
                Sesion.RolActual = usuario.Rol;
                Sesion.AreaActual = usuario.Area;

                // Notify listeners that session changed (so UI can update role-dependent state)
                Sesion.NotifyChanged();

                RegistrarEventoLogin(
                    accion: "Login.Correcto",
                    usuarioAdmin: usuario.NombreUsuario,
                    usuarioAfectado: areaSeleccionada,
                    ok: true,
                    detalles: AuditoriaDetallesHelper.CrearJson(
                        ("Area", areaSeleccionada),
                        ("Usuario", usuario.NombreUsuario)));

                LimpiarIntentosFallidos(nombreUsuario);

                AbrirDashboard(areaSeleccionada);
            }

            finally
            {
                _isLoggingIn = false;
            }
        }

        private void RegistrarEventoLogin(string accion, string usuarioAdmin, string usuarioAfectado, bool ok, string detalles)
        {
            try
            {
                _auditoria.RegistrarEvento(new AuditoriaEvento
                {
                    UsuarioAdmin = usuarioAdmin,
                    Accion = accion,
                    Modulo = "Login",
                    UsuarioAfectado = usuarioAfectado,
                    Resultado = ok,
                    FechaHora = DateTime.Now,
                    Tipo = "Login",
                    Detalles = detalles
                });
            }
            catch
            {
            }
        }

        private static bool EstaBloqueado(
            string nombreUsuario,
            out TimeSpan tiempoRestante)
        {
            string claveUsuario = NormalizarClaveUsuario(nombreUsuario);

            lock (LoginAttemptsLock)
            {
                if (!LoginAttempts.TryGetValue(claveUsuario, out LoginAttemptState? estado) ||
                    estado.LockedUntilUtc is not DateTime lockedUntilUtc)
                {
                    tiempoRestante = TimeSpan.Zero;
                    return false;
                }

                tiempoRestante = lockedUntilUtc - DateTime.UtcNow;
                if (tiempoRestante <= TimeSpan.Zero)
                {
                    LoginAttempts.Remove(claveUsuario);
                    tiempoRestante = TimeSpan.Zero;
                    return false;
                }

                return true;
            }
        }

        private static void RegistrarIntentoFallido(string nombreUsuario)
        {
            string claveUsuario = NormalizarClaveUsuario(nombreUsuario);

            lock (LoginAttemptsLock)
            {
                if (!LoginAttempts.TryGetValue(claveUsuario, out LoginAttemptState? estado))
                {
                    estado = new LoginAttemptState();
                    LoginAttempts[claveUsuario] = estado;
                }

                estado.FailedAttempts++;
                if (estado.FailedAttempts >= MaxFailedAttempts)
                {
                    estado.LockedUntilUtc = DateTime.UtcNow.Add(LoginLockoutDuration);
                }
            }
        }

        private static void LimpiarIntentosFallidos(string nombreUsuario)
        {
            string claveUsuario = NormalizarClaveUsuario(nombreUsuario);

            lock (LoginAttemptsLock)
            {
                LoginAttempts.Remove(claveUsuario);
            }
        }

        private static string NormalizarClaveUsuario(string nombreUsuario)
        {
            return (nombreUsuario ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static bool TienePermisoParaArea(
            Models.Usuario usuario,
            string areaSeleccionada)
        {
            return string.Equals(
                       usuario.Rol?.Trim(),
                       areaSeleccionada,
                       StringComparison.OrdinalIgnoreCase)
                   ||
                   string.Equals(
                       usuario.Area?.Trim(),
                       areaSeleccionada,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void AbrirDashboard(string areaSeleccionada)
        {
            if (Application.Current.MainWindow is not MainWindow ventanaPrincipal)
            {
                return;
            }

            switch (areaSeleccionada.ToLowerInvariant())
            {
                case "administración":
                case "administracion":
                    var dashboardAdministracion =
                        App.DashboardViewModel
                        ?? new DashboardViewModel();

                    App.DashboardViewModel = dashboardAdministracion;

                    ventanaPrincipal.Content = new DashboardView
                    {
                        DataContext = dashboardAdministracion
                    };
                    break;

                case "recepción":
                case "recepcion":
                    ventanaPrincipal.Content = new DashboardRecepcionView
                    {
                        DataContext = new DashboardRecepcionViewModel()
                    };
                    break;

                case "médico":
                case "medico":
                    MostrarMensaje(
                        "El área Médico todavía no está disponible.",
                        "Área no disponible",
                        MessageBoxImage.Information);
                    break;

                default:
                    MostrarMensaje(
                        "El área seleccionada no está configurada.",
                        "Error de configuración",
                        MessageBoxImage.Error);
                    break;
            }
        }

        private static void MostrarMensaje(
            string mensaje,
            string titulo,
            MessageBoxImage icono)
        {
            try
            {
                if (icono == MessageBoxImage.Error)
                {
                    Services.DialogHelper.ShowError(titulo, mensaje);
                }
                else if (icono == MessageBoxImage.Warning)
                {
                    Services.DialogHelper.ShowWarning(titulo, mensaje);
                }
                else
                {
                    Services.DialogHelper.ShowInfo(titulo, mensaje);
                }
            }
            catch
            {
                // fallback
                try { System.Windows.MessageBox.Show(mensaje, titulo, System.Windows.MessageBoxButton.OK, icono); } catch { }
            }
        }
    }
}
