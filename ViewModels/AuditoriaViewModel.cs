using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System;
using ClinicaLongevidadApp.Commands;
using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Win32;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ClinicaLongevidadApp.ViewModels
{
    public class AuditoriaViewModel : BaseViewModel
    {
        private readonly Services.AuditoriaService? _auditoriaService;
        private readonly Services.AuditAdminService? _adminService;
        public System.Collections.ObjectModel.ObservableCollection<Models.AuditoriaModel> ListaAuditoria { get; } = new();
        private Models.AuditoriaModel? _registroSeleccionado;
        public Models.AuditoriaModel? RegistroSeleccionado
        {
            get => _registroSeleccionado;
            set { _registroSeleccionado = value; OnPropertyChanged(nameof(RegistroSeleccionado)); OnPropertyChanged(nameof(DetalleRegistroFormateado)); }
        }

        public string DetalleRegistroFormateado => RegistroSeleccionado?.Detalles ?? string.Empty;

        public ICommand ActualizarCommand { get; }

        public string MensajeError { get; set; } = string.Empty;
        public int Registros => ListaAuditoria.Count;
        public int RegistrosOk { get; set; }
        public int RegistrosError { get; set; }
        public string TiposEnVista { get; set; } = string.Empty;
        public string TextoBusqueda { get; set; } = string.Empty;
        public System.Collections.ObjectModel.ObservableCollection<string> Usuarios { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<string> Modulos { get; } = new();
        public string? UsuarioSeleccionado { get; set; }
        public string? ModuloSeleccionado { get; set; }
        public bool MostrarSoloOperacionesHerramientas { get; set; }

        public AuditoriaViewModel(Services.AuditoriaService? auditoriaService)
        {
            _auditoriaService = auditoriaService;
            // Minimal initialization; real implementation populates collections and commands
            Usuarios.Add("-- Todos --");
            Modulos.Add("-- Todos --");
            // create admin service using connection string saved in application properties if available
            string? conn = null;
            try { conn = Application.Current.Properties["AuditConnectionString"] as string; } catch { }
            if (!string.IsNullOrWhiteSpace(conn))
            {
                _adminService = new AuditAdminService(conn!);
            }

            ActualizarCommand = new RelayCommand(async _ => await EjecutarActualizarAsync());
        }

        public void Cleanup()
        {
            // Placeholder for cleanup actions (timers, subscriptions)
        }

        private async System.Threading.Tasks.Task EjecutarActualizarAsync()
        {
            try
            {
                ListaAuditoria.Clear();

                if (_adminService is null)
                {
                    MensajeError = "Audit connection string not available.";
                    OnPropertyChanged(nameof(MensajeError));
                    return;
                }

                var rows = await System.Threading.Tasks.Task.Run(() => _adminService.GetRecentAudits(200));

                foreach (var r in rows)
                {
                    var model = new Models.AuditoriaModel();
                    model.Id = r.Id;
                    // try parse datetime
                    if (DateTime.TryParse(r.FechaHora, out var dt)) model.FechaHora = dt; else model.FechaHora = DateTime.MinValue;
                    model.UsuarioAdmin = r.UsuarioAdmin ?? string.Empty;
                    model.Accion = r.Accion ?? string.Empty;
                    model.Modulo = r.Modulo ?? string.Empty;
                    model.Resultado = r.Resultado ?? string.Empty;
                    model.UsuarioAfectado = r.UsuarioAfectado ?? string.Empty;
                    model.Detalles = string.Empty;

                    ListaAuditoria.Add(model);
                }

                OnPropertyChanged(nameof(Registros));
            }
            catch (Exception ex)
            {
                try { MensajeError = ex.Message; OnPropertyChanged(nameof(MensajeError)); } catch { }
            }
        }
    }
}
