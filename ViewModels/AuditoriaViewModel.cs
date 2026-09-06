using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
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
        private readonly AuditoriaService _auditoriaService;
        private readonly List<AuditoriaModel> _todosLosRegistros = [];

        private string _textoBusqueda = string.Empty;
        private bool _mostrarSoloOperacionesHerramientas = false;
        private string _usuarioSeleccionado = "Todos";
        private string _moduloSeleccionado = "Todos";
        private string _accionSeleccionada = "Todas";
        private string _resultadoSeleccionado = "Todos";
        private string _tipoSeleccionado = "Todos";
        private string _rolSeleccionado = "Todos";
        private string _areaSeleccionada = "Todas";
        private string _severidadSeleccionada = "Todas";
        private string _filtroPacienteId = string.Empty;
        private string _filtroCitaId = string.Empty;
        private string _filtroSesionId = string.Empty;
        private DateTime? _fechaDesde;
        private DateTime? _fechaHasta;
        private AuditoriaModel? _registroSeleccionado;
        private string _mensajeError = string.Empty;
        private string _detalleRegistroFormateado = string.Empty;
        private string _snackbarMessage = string.Empty;
        private bool _snackbarVisible;
        private readonly DispatcherTimer _snackbarTimer;
        private bool _isAdmin;
        private string _backupTimeText = string.Empty;
        private string _backupDir = string.Empty;
        private string _scheduledBackupInfo = string.Empty;

        public ObservableCollection<AuditoriaModel> ListaAuditoria { get; } = [];

        public ObservableCollection<string> Usuarios { get; } = [];
        public ObservableCollection<string> Modulos { get; } = [];
        public ObservableCollection<string> Acciones { get; } = [];
        public ObservableCollection<string> Tipos { get; } = [];
        public ObservableCollection<string> Roles { get; } = [];
        public ObservableCollection<string> Areas { get; } = [];
        public ObservableCollection<string> Resultados { get; } =
        [
            "Todos",
            "OK",
            "ERROR"
        ];

        public ObservableCollection<string> Severidades { get; } =
        [
            "Todas",
            "Info",
            "Error"
        ];

        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set => SetProperty(ref _textoBusqueda, value);
        }
        public bool MostrarSoloOperacionesHerramientas
        {
            get => _mostrarSoloOperacionesHerramientas;
            set
            {
                if (SetProperty(ref _mostrarSoloOperacionesHerramientas, value))
                {
                    AplicarFiltros();
                }
            }
        }

        /// <summary>
        /// Cleanup subscriptions and resources when the view/viewmodel is disposed or unloaded.
        /// </summary>
        public void Cleanup()
        {
            try
            {
                ClinicaLongevidadApp.Services.Sesion.SessionChanged -= OnSessionChanged;
            }
            catch
            {
                // ignore
            }
        }

        private void BackupNow()
        {
            try
            {
                var props = Application.Current?.Properties;
                var svc = props?["BackupService"] as BackupService;
                var conn = props?["AuditConnectionString"] as string ?? "Data Source=auditoria.db";
                if (svc is null)
                {
                    svc = new BackupService();
                    if (props != null) props["BackupService"] = svc;
                }

                string? backupDir = string.IsNullOrWhiteSpace(BackupDir) ? null : BackupDir;
                string path = svc!.CreateBackup(conn, backupDir);
                ShowSnackbar($"Copia creada: {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                MensajeError = "Error creando copia: " + ex.Message;
            }
        }

        private void ScheduleBackup()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(BackupTimeText))
                {
                    MensajeError = "Introduzca hora en formato HH:mm";
                    return;
                }

                if (!TimeSpan.TryParse(BackupTimeText, out var time))
                {
                    MensajeError = "Formato de hora inválido. Use HH:mm";
                    return;
                }

                var props = Application.Current?.Properties;
                var svc = props?["BackupService"] as BackupService;
                var conn = props?["AuditConnectionString"] as string ?? "Data Source=auditoria.db";
                if (svc is null)
                {
                    // create and store
                    svc = new BackupService();
                    if (props != null) props["BackupService"] = svc;
                }

                svc!.ScheduleDailyBackup(time, conn, string.IsNullOrWhiteSpace(BackupDir) ? null : BackupDir);
                ScheduledBackupInfo = "Programada diariamente a las " + time.ToString(@"hh\:mm");
                ShowSnackbar("Copia programada correctamente.");
            }
            catch (Exception ex)
            {
                MensajeError = "Error al programar copia: " + ex.Message;
            }
        }

        private void CancelBackup()
        {
            try
            {
                var props = Application.Current?.Properties;
                var svc = props?["BackupService"] as BackupService;
                if (svc is null)
                {
                    MensajeError = "No hay copia programada.";
                    return;
                }

                svc.CancelScheduledBackup();
                if (props != null) props.Remove("BackupService");
                ScheduledBackupInfo = string.Empty;
                ShowSnackbar("Copia programada cancelada.");
            }
            catch (Exception ex)
            {
                MensajeError = "Error cancelando copia: " + ex.Message;
            }
        }

        public string UsuarioSeleccionado
        {
            get => _usuarioSeleccionado;
            set => SetProperty(ref _usuarioSeleccionado, value);
        }

        public string ModuloSeleccionado
        {
            get => _moduloSeleccionado;
            set => SetProperty(ref _moduloSeleccionado, value);
        }

        public string AccionSeleccionada
        {
            get => _accionSeleccionada;
            set => SetProperty(ref _accionSeleccionada, value);
        }

        public string ResultadoSeleccionado
        {
            get => _resultadoSeleccionado;
            set => SetProperty(ref _resultadoSeleccionado, value);
        }

        public string TipoSeleccionado
        {
            get => _tipoSeleccionado;
            set => SetProperty(ref _tipoSeleccionado, value);
        }

        public string SeveridadSeleccionada
        {
            get => _severidadSeleccionada;
            set => SetProperty(ref _severidadSeleccionada, value);
        }

        public string RolSeleccionado
        {
            get => _rolSeleccionado;
            set => SetProperty(ref _rolSeleccionado, value);
        }

        public string AreaSeleccionada
        {
            get => _areaSeleccionada;
            set => SetProperty(ref _areaSeleccionada, value);
        }

        public string FiltroPacienteId
        {
            get => _filtroPacienteId;
            set => SetProperty(ref _filtroPacienteId, value);
        }

        public string FiltroCitaId
        {
            get => _filtroCitaId;
            set => SetProperty(ref _filtroCitaId, value);
        }

        public string FiltroSesionId
        {
            get => _filtroSesionId;
            set => SetProperty(ref _filtroSesionId, value);
        }

        public DateTime? FechaDesde
        {
            get => _fechaDesde;
            set => SetProperty(ref _fechaDesde, value);
        }

        public DateTime? FechaHasta
        {
            get => _fechaHasta;
            set => SetProperty(ref _fechaHasta, value);
        }

        public AuditoriaModel? RegistroSeleccionado
        {
            get => _registroSeleccionado;
            set
            {
                if (SetProperty(ref _registroSeleccionado, value))
                {
                    DetalleRegistroFormateado = FormatearDetalles(value?.Detalles);
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private void CopiarKeyVersions()
        {
            try
            {
                if (RegistroSeleccionado is null) return;

                string hv = RegistroSeleccionado.KeyVersion ?? string.Empty;
                string ev = RegistroSeleccionado.KeyVersionEnc ?? string.Empty;
                string texto = $"HMAC: {hv}    ENC: {ev}";
                Clipboard.SetText(texto);
                ShowSnackbar("Versiones de clave copiadas al portapapeles.");
                LogService.Info("AuditoriaViewModel", "Copied key versions to clipboard.");
            }
            catch
            {
                // No interrumpir la UI si falla el copiado
                try { ShowSnackbar("No se pudo copiar versiones de clave."); } catch { }
                LogService.Warning("AuditoriaViewModel", "Failed to copy key versions to clipboard.");
            }
        }

        public string DetalleRegistroFormateado
        {
            get => _detalleRegistroFormateado;
            set => SetProperty(ref _detalleRegistroFormateado, value);
        }

        public string MensajeError
        {
            get => _mensajeError;
            set => SetProperty(ref _mensajeError, value);
        }

        public int Registros => ListaAuditoria.Count;
        public int RegistrosOk => ListaAuditoria.Count(r => string.Equals(r.Resultado, "OK", StringComparison.OrdinalIgnoreCase));
        public int RegistrosError => ListaAuditoria.Count(r => string.Equals(r.Resultado, "ERROR", StringComparison.OrdinalIgnoreCase));
        public int TiposEnVista => ListaAuditoria
            .Select(r => r.Tipo)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        public RelayCommand AplicarFiltrosCommand { get; }
        public RelayCommand LimpiarFiltrosCommand { get; }
        public RelayCommand ActualizarCommand { get; }
        public RelayCommand ExportarCsvCommand { get; }
        public RelayCommand CopiarSesionIdCommand { get; }
        public RelayCommand CopiarKeyVersionsCommand { get; }
        public RelayCommand RotateHmacCommand { get; }
        public RelayCommand RotateEncCommand { get; }
        public RelayCommand BackupNowCommand { get; }
        public RelayCommand ScheduleBackupCommand { get; }
        public RelayCommand CancelBackupCommand { get; }

        public AuditoriaViewModel(AuditoriaService auditoriaService)
        {
            _auditoriaService = auditoriaService ?? throw new ArgumentNullException(nameof(auditoriaService));

            AplicarFiltrosCommand = new RelayCommand(_ => AplicarFiltros());
            LimpiarFiltrosCommand = new RelayCommand(_ => LimpiarFiltros());
            ActualizarCommand = new RelayCommand(_ => CargarAuditoria());
            ExportarCsvCommand = new RelayCommand(_ => ExportarCsv());
            CopiarSesionIdCommand = new RelayCommand(
                _ => CopiarSesionId(),
                _ => RegistroSeleccionado is not null &&
                     !string.IsNullOrWhiteSpace(RegistroSeleccionado.SesionId));

            CopiarKeyVersionsCommand = new RelayCommand(
                _ => CopiarKeyVersions(),
                _ => RegistroSeleccionado is not null &&
                     (!string.IsNullOrWhiteSpace(RegistroSeleccionado.KeyVersion) || !string.IsNullOrWhiteSpace(RegistroSeleccionado.KeyVersionEnc)));

            RotateHmacCommand = new RelayCommand(_ => RotateHmac(), _ => CanRotateKeys());
            RotateEncCommand = new RelayCommand(_ => RotateEnc(), _ => CanRotateKeys());
            BackupNowCommand = new RelayCommand(_ => BackupNow(), _ => IsAdmin);
            ScheduleBackupCommand = new RelayCommand(_ => ScheduleBackup(), _ => IsAdmin);
            CancelBackupCommand = new RelayCommand(_ => CancelBackup(), _ => IsAdmin);

            CargarAuditoria();

            // Initialize admin flag based on current session
            _isAdmin = DetermineIsAdmin();

            // Subscribe to session changes to update IsAdmin dynamically
            try
            {
                ClinicaLongevidadApp.Services.Sesion.SessionChanged += OnSessionChanged;
            }
            catch
            {
                // ignore subscription failures
            }


            _snackbarTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _snackbarTimer.Tick += (s, e) =>
            {
                SnackbarVisible = false;
                SnackbarMessage = string.Empty;
                _snackbarTimer.Stop();
            };

            BackupNowCommand = new RelayCommand(_ => BackupNow(), _ => IsAdmin);
            ScheduleBackupCommand = new RelayCommand(_ => ScheduleBackup(), _ => IsAdmin);
            CancelBackupCommand = new RelayCommand(_ => CancelBackup(), _ => IsAdmin);
        }

        private void OnSessionChanged()
        {
            try
            {
                IsAdmin = DetermineIsAdmin();
                // Update command availability
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
            catch
            {
                // ignore
            }
        }

        private bool DetermineIsAdmin()
        {
            try
            {
                var rol = Sesion.RolActual;
                if (string.IsNullOrWhiteSpace(rol)) return false;

                return string.Equals(rol, "administración", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rol, "administracion", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rol, "admin", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rol, "administrador", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            private set => SetProperty(ref _isAdmin, value);
        }

        public string BackupTimeText
        {
            get => _backupTimeText;
            set => SetProperty(ref _backupTimeText, value);
        }

        public string BackupDir
        {
            get => _backupDir;
            set => SetProperty(ref _backupDir, value);
        }

        public string ScheduledBackupInfo
        {
            get => _scheduledBackupInfo;
            set => SetProperty(ref _scheduledBackupInfo, value);
        }

        private bool CanRotateKeys()
        {
            try
            {
                var svc = Application.Current?.Properties["KeyRotationService"] as KeyRotationService;
                return svc is not null && IsAdmin;
            }
            catch
            {
                return false;
            }
        }

        private void RotateHmac()
        {
            try
            {
                var svc = Application.Current?.Properties["KeyRotationService"] as KeyRotationService;
                if (svc is null)
                {
                    MensajeError = "No hay un servicio de rotación configurado.";
                    return;
                }

                var resp = MessageBox.Show("Está a punto de rotar la clave HMAC. Esta acción generará un nuevo secreto y quedará registrada en la auditoría. ¿Desea continuar?", "Confirmar rotación HMAC", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (resp != MessageBoxResult.Yes)
                {
                    return;
                }

                svc.RotateHmacKey();
                ShowSnackbar("Rotación HMAC iniciada. Revise la auditoría para verificar la versión del secreto.");

                // Refresh audit list shortly after rotation so the new event becomes visible
                try
                {
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(300);
                        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => CargarAuditoria()));
                    });
                }
                catch
                {
                    // ignore refresh failures
                }
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al rotar HMAC: {ex.Message}";
            }
        }

        private void RotateEnc()
        {
            try
            {
                var svc = Application.Current?.Properties["KeyRotationService"] as KeyRotationService;
                if (svc is null)
                {
                    MensajeError = "No hay un servicio de rotación configurado.";
                    return;
                }

                var resp = MessageBox.Show("Está a punto de rotar la clave de encriptación. Esta acción generará un nuevo secreto y quedará registrada en la auditoría. ¿Desea continuar?", "Confirmar rotación ENC", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (resp != MessageBoxResult.Yes)
                {
                    return;
                }

                svc.RotateEncryptionKey();
                ShowSnackbar("Rotación ENC iniciada. Revise la auditoría para verificar la versión del secreto.");

                // Refresh audit list shortly after rotation so the new event becomes visible
                try
                {
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(300);
                        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => CargarAuditoria()));
                    });
                }
                catch
                {
                    // ignore refresh failures
                }
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al rotar ENC: {ex.Message}";
            }
        }

        private void CargarAuditoria()
        {
            try
            {
                MensajeError = string.Empty;

                var registros = _auditoriaService.ObtenerAuditoria();

                _todosLosRegistros.Clear();
                _todosLosRegistros.AddRange(registros);

// No-op edit to ensure context
                CargarOpcionesDeFiltro();
                AplicarFiltros();
            }
            catch (Exception ex)
            {
                ListaAuditoria.Clear();
                RegistroSeleccionado = null;
                OnPropertyChanged(nameof(Registros));
                OnPropertyChanged(nameof(RegistrosOk));
                OnPropertyChanged(nameof(RegistrosError));
                OnPropertyChanged(nameof(TiposEnVista));

                MensajeError = $"No se pudo cargar la auditoría: {ex.Message}";
            }
        }

        private void CargarOpcionesDeFiltro()
        {
            Usuarios.Clear();
            Usuarios.Add("Todos");

            foreach (string usuario in _todosLosRegistros
                .Select(registro => registro.UsuarioAdmin)
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(valor => valor))
            {
                Usuarios.Add(usuario);
            }

            Modulos.Clear();
            Modulos.Add("Todos");

            foreach (string modulo in _todosLosRegistros
                .Select(registro => registro.Modulo)
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(valor => valor))
            {
                Modulos.Add(modulo);
            }

            Acciones.Clear();
            Acciones.Add("Todas");

            foreach (string accion in _todosLosRegistros
                .Select(registro => registro.Accion)
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(valor => valor))
            {
                Acciones.Add(accion);
            }

            Tipos.Clear();
            Tipos.Add("Todos");

            foreach (string tipo in _todosLosRegistros
                .Select(registro => registro.Tipo)
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(valor => valor))
            {
                Tipos.Add(tipo);
            }

            Roles.Clear();
            Roles.Add("Todos");

            foreach (string rol in _todosLosRegistros
                .Select(registro => registro.Rol)
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(valor => valor))
            {
                Roles.Add(rol);
            }

            Areas.Clear();
            Areas.Add("Todas");

            foreach (string area in _todosLosRegistros
                .Select(registro => registro.Area)
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(valor => valor))
            {
                Areas.Add(area);
            }

            UsuarioSeleccionado = "Todos";
            ModuloSeleccionado = "Todos";
            AccionSeleccionada = "Todas";
            ResultadoSeleccionado = "Todos";
            TipoSeleccionado = "Todos";
            RolSeleccionado = "Todos";
            AreaSeleccionada = "Todas";
            SeveridadSeleccionada = "Todas";
        }

        private void AplicarFiltros()
        {
            IEnumerable<AuditoriaModel> registros = _todosLosRegistros;

            if (!string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                string texto = TextoBusqueda.Trim();

                registros = registros.Where(registro =>
                    Contiene(registro.UsuarioAdmin, texto) ||
                    Contiene(registro.Accion, texto) ||
                    Contiene(registro.Modulo, texto) ||
                    Contiene(registro.UsuarioAfectado, texto) ||
                    Contiene(registro.Resultado, texto) ||
                    Contiene(registro.Tipo, texto) ||
                    Contiene(registro.Rol, texto) ||
                    Contiene(registro.Area, texto) ||
                    Contiene(registro.Equipo, texto) ||
                    Contiene(registro.VersionApp, texto) ||
                    Contiene(registro.SesionId, texto) ||
                    Contiene(registro.Detalles, texto));
            }

            if (!string.Equals(UsuarioSeleccionado, "Todos", StringComparison.OrdinalIgnoreCase))
            {
                registros = registros.Where(registro =>
                    string.Equals(registro.UsuarioAdmin, UsuarioSeleccionado, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(ModuloSeleccionado, "Todos", StringComparison.OrdinalIgnoreCase))
            {
                registros = registros.Where(registro =>
                    string.Equals(registro.Modulo, ModuloSeleccionado, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(AccionSeleccionada, "Todas", StringComparison.OrdinalIgnoreCase))
            {
                registros = registros.Where(registro =>
                    string.Equals(registro.Accion, AccionSeleccionada, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(ResultadoSeleccionado, "Todos", StringComparison.OrdinalIgnoreCase))
            {
                registros = registros.Where(registro =>
                    string.Equals(registro.Resultado, ResultadoSeleccionado, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(TipoSeleccionado, "Todos", StringComparison.OrdinalIgnoreCase))
            {
                registros = registros.Where(registro =>
                    string.Equals(registro.Tipo, TipoSeleccionado, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(RolSeleccionado, "Todos", StringComparison.OrdinalIgnoreCase))
            {
                registros = registros.Where(registro =>
                    string.Equals(registro.Rol, RolSeleccionado, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(AreaSeleccionada, "Todas", StringComparison.OrdinalIgnoreCase))
            {
                registros = registros.Where(registro =>
                    string.Equals(registro.Area, AreaSeleccionada, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(SeveridadSeleccionada, "Todas", StringComparison.OrdinalIgnoreCase))
            {
                registros = registros.Where(registro =>
                    string.Equals(ObtenerSeveridad(registro), SeveridadSeleccionada, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(FiltroPacienteId))
            {
                string valor = FiltroPacienteId.Trim();
                registros = registros.Where(registro =>
                    AuditoriaDetallesHelper.CoincideCampo(registro.Detalles, "PacienteId", valor) ||
                    Contiene(registro.UsuarioAfectado, valor));
            }

            if (!string.IsNullOrWhiteSpace(FiltroCitaId))
            {
                string valor = FiltroCitaId.Trim();
                registros = registros.Where(registro =>
                    AuditoriaDetallesHelper.CoincideCampo(registro.Detalles, "CitaId", valor));
            }

            if (!string.IsNullOrWhiteSpace(FiltroSesionId))
            {
                string valor = FiltroSesionId.Trim();
                registros = registros.Where(registro =>
                    Contiene(registro.SesionId, valor) ||
                    AuditoriaDetallesHelper.CoincideCampo(registro.Detalles, "SesionId", valor));
            }

            if (FechaDesde.HasValue)
            {
                DateTime fechaDesde = FechaDesde.Value.Date;
                registros = registros.Where(registro => registro.FechaHora.Date >= fechaDesde);
            }

            if (FechaHasta.HasValue)
            {
                DateTime fechaHasta = FechaHasta.Value.Date;
                registros = registros.Where(registro => registro.FechaHora.Date <= fechaHasta);
            }

            ListaAuditoria.Clear();

            foreach (AuditoriaModel registro in registros)
            {
                ListaAuditoria.Add(registro);
            }

            if (RegistroSeleccionado is not null && !ListaAuditoria.Contains(RegistroSeleccionado))
            {
                RegistroSeleccionado = null;
            }

            OnPropertyChanged(nameof(Registros));
            OnPropertyChanged(nameof(RegistrosOk));
            OnPropertyChanged(nameof(RegistrosError));
            OnPropertyChanged(nameof(TiposEnVista));
        }

        private void LimpiarFiltros()
        {
            TextoBusqueda = string.Empty;
            UsuarioSeleccionado = "Todos";
            ModuloSeleccionado = "Todos";
            AccionSeleccionada = "Todas";
            ResultadoSeleccionado = "Todos";
            TipoSeleccionado = "Todos";
            RolSeleccionado = "Todos";
            AreaSeleccionada = "Todas";
            SeveridadSeleccionada = "Todas";
            FiltroPacienteId = string.Empty;
            FiltroCitaId = string.Empty;
            FiltroSesionId = string.Empty;
            FechaDesde = null;
            FechaHasta = null;

            AplicarFiltros();
        }

        private void ExportarCsv()
        {
            if (ListaAuditoria.Count == 0)
            {
                MensajeError = "No hay registros para exportar.";
                return;
            }

            var dialogo = new SaveFileDialog
            {
                Title = "Exportar auditoría",
                Filter = "Archivo CSV (*.csv)|*.csv",
                FileName = $"Auditoria_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialogo.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var contenido = new StringBuilder();

                contenido.AppendLine(
                    "Id;FechaHora;UsuarioAdmin;Accion;" +
                    "Modulo;UsuarioAfectado;Resultado;Tipo;Rol;Area;SesionId;Equipo;VersionApp;Severidad;Detalles");

                foreach (AuditoriaModel registro in ListaAuditoria)
                {
                    contenido.AppendLine(string.Join(
                        ";",
                        Escapar(registro.Id.ToString()),
                        Escapar(registro.FechaHora.ToString("dd/MM/yyyy HH:mm:ss")),
                        Escapar(registro.UsuarioAdmin),
                        Escapar(registro.Accion),
                        Escapar(registro.Modulo),
                        Escapar(registro.UsuarioAfectado),
                        Escapar(registro.Resultado),
                        Escapar(registro.Tipo),
                        Escapar(registro.Rol),
                        Escapar(registro.Area),
                        Escapar(registro.SesionId),
                        Escapar(registro.Equipo),
                        Escapar(registro.VersionApp),
                        Escapar(ObtenerSeveridad(registro)),
                        Escapar(registro.Detalles)));
                }

                File.WriteAllText(dialogo.FileName, contenido.ToString(), new UTF8Encoding(true));

                MensajeError = string.Empty;
            }
            catch (Exception ex)
            {
                MensajeError = $"No se pudo exportar la auditoría: {ex.Message}";
            }
        }

        private void CopiarSesionId()
        {
            if (RegistroSeleccionado is null || string.IsNullOrWhiteSpace(RegistroSeleccionado.SesionId))
            {
                return;
            }

            try
            {
                Clipboard.SetText(RegistroSeleccionado.SesionId);
                ShowSnackbar("Identificador de sesión copiado al portapapeles.");
                LogService.Info("AuditoriaViewModel", "Copied session id to clipboard.");
            }
            catch (Exception ex)
            {
                MensajeError = $"No se pudo copiar SesiónId: {ex.Message}";
                try { ShowSnackbar("No se pudo copiar Identificador de sesión."); } catch { }
                LogService.Error("AuditoriaViewModel", "Failed to copy session id to clipboard.", ex);
            }
        }

        public string SnackbarMessage
        {
            get => _snackbarMessage;
            set => SetProperty(ref _snackbarMessage, value);
        }

        public bool SnackbarVisible
        {
            get => _snackbarVisible;
            set => SetProperty(ref _snackbarVisible, value);
        }

        private void ShowSnackbar(string message)
        {
            try
            {
                SnackbarMessage = message;
                SnackbarVisible = true;
                try { _snackbarTimer.Stop(); } catch { }
                _snackbarTimer.Start();
            }
            catch
            {
                // ignore UI failures in tests/headless
            }
        }

        private static string ObtenerSeveridad(AuditoriaModel registro)
        {
            return string.Equals(registro.Resultado, "ERROR", StringComparison.OrdinalIgnoreCase)
                ? "Error"
                : "Info";
        }

        private static string FormatearDetalles(string? detalles)
        {
            if (string.IsNullOrWhiteSpace(detalles))
            {
                return string.Empty;
            }

            string texto = detalles.Trim();
            if (!texto.StartsWith("{", StringComparison.Ordinal))
            {
                return texto;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(texto);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch
            {
                return texto;
            }
        }

        private static string Escapar(string? valor)
        {
            if (string.IsNullOrEmpty(valor))
            {
                return string.Empty;
            }

            string resultado = valor.Replace("\"", "\"\"", StringComparison.Ordinal);
            return $"\"{resultado}\"";
        }

        private static bool Contiene(string? valor, string texto)
        {
            return !string.IsNullOrWhiteSpace(valor) &&
                   valor.Contains(texto, StringComparison.OrdinalIgnoreCase);
        }
    }
}