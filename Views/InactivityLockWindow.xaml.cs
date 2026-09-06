using System;
using System.Windows;
using System.Windows.Controls;
using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.ViewModels;

namespace ClinicaLongevidadApp.Views
{
    public partial class InactivityLockWindow : Window
    {
        private bool _unlockAllowed;
        private string _password = string.Empty;

        public InactivityLockWindow()
        {
            InitializeComponent();
            UsuarioBloqueadoText.Text = $"Usuario: {Sesion.UsuarioActual ?? "Sistema"}";
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _password = passwordBox.Password;
            }
        }

        private void Desbloquear_Click(object sender, RoutedEventArgs e)
        {
            string usuarioActual = Sesion.UsuarioActual ?? string.Empty;

            if (string.IsNullOrWhiteSpace(usuarioActual))
            {
                MensajeErrorText.Text = "No existe una sesión activa para desbloquear.";
                return;
            }

            if (!UsuarioService.ValidarLogin(usuarioActual, _password))
            {
                RegistrarEventoDesbloqueo(usuarioActual, false, "Contraseña incorrecta");

                MensajeErrorText.Text = "Contraseña incorrecta.";
                PasswordBox.Clear();
                return;
            }

            RegistrarEventoDesbloqueo(usuarioActual, true, null);

            _unlockAllowed = true;
            DialogResult = true;
            Close();
        }

        private static void RegistrarEventoDesbloqueo(string usuarioActual, bool ok, string? motivo)
        {
            try
            {
                App.AuditoriaService?.RegistrarEvento(new AuditoriaEvento
                {
                    UsuarioAdmin = usuarioActual,
                    Accion = ok ? "Sesion.DesbloqueoInactividad" : "Sesion.DesbloqueoFallido",
                    Modulo = "Seguridad",
                    UsuarioAfectado = usuarioActual,
                    Resultado = ok,
                    FechaHora = DateTime.Now,
                    Tipo = "Sesion",
                    Detalles = AuditoriaDetallesHelper.CrearJson(
                        ("Usuario", usuarioActual),
                        ("Motivo", motivo ?? string.Empty))
                });
            }
            catch
            {
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_unlockAllowed)
            {
                e.Cancel = true;
            }

            base.OnClosing(e);
        }
    }
}
