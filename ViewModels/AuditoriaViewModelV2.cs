using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Commands;
using ClinicaLongevidadApp.Services;
// using ClinicaLongevidadApp.Services.KeyRotation; // fully qualify to avoid ambiguity with Services.KeyRotationService

namespace ClinicaLongevidadApp.ViewModels
{
    // A lean, testable reimplementation of the Auditoria view model.
    public class AuditoriaViewModelV2 : BaseViewModel
    {
        private readonly AuditoriaService? _auditoriaService;
        private readonly AuditAdminService? _adminService;

        // Full dataset cache
        private List<AuditoriaModel> _rowsAll = new List<AuditoriaModel>();

        // Public collections bound to the UI
        public ObservableCollection<AuditoriaModel> ListaAuditoria { get; } = new();
        public ObservableCollection<string> Usuarios { get; } = new();
        public ObservableCollection<string> Modulos { get; } = new();
        public ObservableCollection<string> Acciones { get; } = new();
        public ObservableCollection<string> Resultados { get; } = new();
        public ObservableCollection<string> Tipos { get; } = new();
        public ObservableCollection<string> Roles { get; } = new();
        public ObservableCollection<string> Areas { get; } = new();

        private AuditoriaModel? _registroSeleccionado;
        public AuditoriaModel? RegistroSeleccionado
        {
            get => _registroSeleccionado;
            set
            {
                _registroSeleccionado = value;
                OnPropertyChanged(nameof(RegistroSeleccionado));
                OnPropertyChanged(nameof(DetalleRegistroFormateado));
            }
        }

        // Persist scheduled time to disk so schedule can be restored after restart
        private void SaveScheduledTime(string timeText)
        {
            try
            {
                var baseDir = ClinicaLongevidadApp.Services.AppPaths.BaseDir;
                var file = System.IO.Path.Combine(baseDir, "backup_schedule.json");
                var obj = new { Time = timeText };
                var txt = System.Text.Json.JsonSerializer.Serialize(obj);
                System.IO.File.WriteAllText(file, txt, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        private string? LoadScheduledTime()
        {
            try
            {
                var file = System.IO.Path.Combine(ClinicaLongevidadApp.Services.AppPaths.BaseDir, "backup_schedule.json");
                if (!System.IO.File.Exists(file)) return null;
                var txt = System.IO.File.ReadAllText(file, System.Text.Encoding.UTF8);
                using var doc = System.Text.Json.JsonDocument.Parse(txt);
                if (doc.RootElement.TryGetProperty("Time", out var t)) return t.GetString();
            }
            catch { }
            return null;
        }

        private void ForceScheduledNow()
        {
            // Trigger a scheduled backup without UI modal dialogs (used by timer)
            try
            {
                _ = PerformBackupAsync(showDialog: false);
            }
            catch { }
        }

        private async Task PerformBackupAsync(bool showDialog)
        {
            try
            {
                var conn = Application.Current.Properties["AuditConnectionString"] as string;
                if (string.IsNullOrWhiteSpace(conn))
                {
                    var dbPath = System.IO.Path.Combine(ClinicaLongevidadApp.Services.AppPaths.BaseDir, "ClinicaLongevidad.db");
                    conn = $"Data Source={dbPath}";
                }
                var backupDir = ClinicaLongevidadApp.Services.AppPaths.BackupsDir;

                string? created = null;
                try
                {
                    // Run the service call in background to avoid blocking UI thread
                    created = await Task.Run(() => _backupService?.TriggerImmediateBackup(conn, backupDir));
                    try { WriteVmDebugLog($"PerformBackupAsync: created '{created}'"); } catch { }
                }
                catch (Exception ex)
                {
                    try { WriteVmDebugLog($"PerformBackupAsync failed: {ex.Message}"); } catch { }
                }

                // Notify UI
                try
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(created))
                        {
                            ShowSnackbar($"Copia creada en: {created}", 6);
                            if (showDialog)
                            {
                                try { Services.DialogHelper.ShowInfo("Backup", $"Copia creada:\n{created}"); } catch { }
                            }
                        }
                        else
                        {
                            ShowSnackbar("Error creando copia programada", 5);
                        }
                    });
                }
                catch { }
            }
            catch { }
        }

        private void UpdateMetrics(IEnumerable<AuditoriaModel> rows)
        {
            try
            {
                var list = (rows ?? Enumerable.Empty<AuditoriaModel>()).ToList();
                Registros = list.Count;
                RegistrosOk = list.Count(r => string.Equals((r.Resultado ?? string.Empty).Trim(), "OK", StringComparison.OrdinalIgnoreCase) || string.Equals((r.Resultado ?? string.Empty).Trim(), "True", StringComparison.OrdinalIgnoreCase));
                RegistrosError = Registros - RegistrosOk;
                TiposEnVista = list.Select(r => (r.Tipo ?? string.Empty).Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                MensajeError = string.Empty;
            }
            catch { }
        }

        private async Task BackupNowAsync()
        {
            try
            {
                await PerformBackupAsync(showDialog: true);
            }
            catch { }
        }

        private void ScheduleBackup()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(BackupTimeText))
                {
                    ShowSnackbar("Introduce una hora válida (HH:mm)", 4);
                    return;
                }

                // Robust parse: try DateTime then TimeSpan with common formats
                TimeSpan ts;
                var parsed = false;
                try
                {
                    if (DateTime.TryParseExact(BackupTimeText.Trim(), new[] { "HH:mm", "H:mm", "hh:mm", "h:mm" }, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var dt))
                    {
                        ts = dt.TimeOfDay;
                        parsed = true;
                    }
                    else if (DateTime.TryParse(BackupTimeText.Trim(), out var dt2))
                    {
                        ts = dt2.TimeOfDay;
                        parsed = true;
                    }
                    else if (TimeSpan.TryParseExact(BackupTimeText.Trim(), new[] { "hh\\:mm", "h\\:mm" }, System.Globalization.CultureInfo.InvariantCulture, out var tsv))
                    {
                        ts = tsv;
                        parsed = true;
                    }
                    else if (TimeSpan.TryParse(BackupTimeText.Trim(), out var ts2))
                    {
                        ts = ts2;
                        parsed = true;
                    }
                    else
                    {
                        ts = TimeSpan.Zero;
                    }
                }
                catch { ts = TimeSpan.Zero; }

                if (!parsed)
                {
                    ShowSnackbar("Formato de hora inválido. Usa HH:mm", 4);
                    try { WriteVmDebugLog($"ScheduleBackup: parse failed for '{BackupTimeText}'"); } catch { }
                    return;
                }

                var conn = Application.Current.Properties["AuditConnectionString"] as string;
                if (string.IsNullOrWhiteSpace(conn))
                {
                    var dbPath = System.IO.Path.Combine(ClinicaLongevidadApp.Services.AppPaths.BaseDir, "ClinicaLongevidad.db");
                    conn = $"Data Source={dbPath}";
                }

                var backupDir = ClinicaLongevidadApp.Services.AppPaths.BackupsDir;

                // Persist the selected time. If the independent runner is installed,
                // delegate execution to Windows so closing the WPF app does not stop it.
                var tsString = ts.ToString(@"hh\:mm");

                // if already scheduled to the same time, ignore duplicate requests
                try
                {
                    if (IsBackupScheduled && !string.IsNullOrWhiteSpace(_savedBackupTime) && string.Equals(_savedBackupTime, tsString, StringComparison.Ordinal))
                    {
                        ShowSnackbar($"Ya existe una copia programada a las {tsString}", 4);
                        try { WriteVmDebugLog($"ScheduleBackup: ignored duplicate schedule request for {tsString}"); } catch { }
                        return;
                    }
                }
                catch { }

                // if there is an existing different schedule, cancel it first to avoid multiple timers
                try
                {
                    if (IsBackupScheduled && (!string.IsNullOrWhiteSpace(_savedBackupTime) && !string.Equals(_savedBackupTime, tsString, StringComparison.Ordinal)))
                    {
                        try { WriteVmDebugLog($"ScheduleBackup: cancelling previous schedule {_savedBackupTime} before applying new {tsString}"); } catch { }
                        try { _backupService?.CancelScheduledBackup(); } catch { }
                    }
                }
                catch { }

                BackupTimeText = tsString;
                SaveScheduledTime(BackupTimeText);
                DateTime? svcNext = null;
                if (WindowsScheduledBackupTaskService.IsRunnerInstalled)
                {
                    WindowsScheduledBackupTaskService.ScheduleDaily(ts);
                }
                else
                {
                    try { WriteVmDebugLog($"ScheduleBackup: registering in-process schedule at {BackupTimeText}"); } catch { }
                    svcNext = _backupService?.ScheduleDailyBackup(ts, conn, backupDir);
                }

                // compute next run local datetime similarly to BackupService and start VM-level timer
                try
                {
                    if (svcNext.HasValue)
                    {
                        NextScheduledRun = svcNext.Value;
                    }
                    else
                    {
                        var now = DateTime.Now;
                        var next = new DateTime(now.Year, now.Month, now.Day, ts.Hours, ts.Minutes, 0);
                        if (next <= now) next = next.AddDays(1);
                        NextScheduledRun = next;
                    }

                    IsBackupScheduled = true;
                    // remember the scheduled value so UI edits can detect changes
                    try { _savedBackupTime = BackupTimeText; } catch { }
                }
                catch { NextScheduledRun = null; IsBackupScheduled = true; }

                ShowSnackbar($@"Copia programada a las {ts:hh\:mm}", 5);
                BackupScheduleStatus = "Programación diaria activa.";
            }
                catch (Exception ex)
            {
                BackupScheduleStatus = "No se pudo programar la copia: " + ex.Message;
                try { Services.DialogHelper.ShowError("Backup", "Error al programar copia: " + ex.Message); } catch { }
            }
        }

        private void CancelScheduledBackup()
        {
            try
            {
                _backupService?.CancelScheduledBackup();
                if (WindowsScheduledBackupTaskService.IsTaskInstalled())
                    WindowsScheduledBackupTaskService.CancelDaily();
                IsBackupScheduled = false;
                NextScheduledRun = null;
                SaveScheduledTime(string.Empty);
                _savedBackupTime = null;
                BackupScheduleStatus = "No hay copia programada.";
                ShowSnackbar("Copia programada cancelada", 4);
            }
            catch { }
        }

        private void ToggleSchedule()
        {
            try
            {
                // Debug visibility was removed to avoid modal dialogs during normal use
                if (IsBackupScheduled) CancelScheduledBackup();
                else ScheduleBackup();
            }
            catch { }
        }

        private async Task ScheduleTestInOneMinuteAsync()
        {
            if (_testBackupCancellation is not null)
            {
                ShowSnackbar("Ya hay una copia de prueba pendiente.", 4);
                return;
            }

            var cancellation = new System.Threading.CancellationTokenSource();
            _testBackupCancellation = cancellation;
            IsTestBackupScheduled = true;
            try
            {
                // Schedule a one-off test backup in one minute without registering a recurring schedule
                var when = DateTime.Now.AddMinutes(1);
                NextScheduledRun = new DateTime(when.Year, when.Month, when.Day, when.Hour, when.Minute, 0);
                ShowSnackbar($"Copia de prueba (única) en {NextScheduledRun:dd/MM/yyyy HH:mm}", 5);
                BackupScheduleStatus = "Copia de prueba única pendiente.";

                // Keep the continuation on the WPF synchronization context. PerformBackupAsync
                // reads Application.Current properties, which must not be accessed from a worker thread.
                await Task.Delay(TimeSpan.FromMinutes(1), cancellation.Token);
                await PerformBackupAsync(showDialog: true);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex)
            {
                try { Services.DialogHelper.ShowError("Backup", "Error al programar copia de prueba: " + ex.Message); } catch { }
            }
            finally
            {
                if (ReferenceEquals(_testBackupCancellation, cancellation))
                {
                    _testBackupCancellation = null;
                    IsTestBackupScheduled = false;
                    NextScheduledRun = null;
                }
                cancellation.Dispose();
            }
        }

        private void CancelTestBackup()
        {
            var cancellation = _testBackupCancellation;
            if (cancellation is null) return;

            try
            {
                cancellation.Cancel();
                BackupScheduleStatus = "Copia de prueba cancelada.";
                ShowSnackbar("Copia de prueba cancelada", 4);
            }
            catch { }
        }

        private void WriteVmDebugLog(string message)
        {
            try
            {
                var baseDir = ClinicaLongevidadApp.Services.AppPaths.LogsDir;
                var file = System.IO.Path.Combine(baseDir, "backup_vm.log");
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n";
                System.IO.File.AppendAllText(file, line, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        private async Task RestoreBackupAsync()
        {
            try
            {
                await Task.CompletedTask;
                var backupDir = ClinicaLongevidadApp.Services.AppPaths.BackupsDir;
                var dlg = new Microsoft.Win32.OpenFileDialog() { Filter = "SQLite backups (*.db;*.sqlite;*.sqlite3;*.bak)|*.db;*.sqlite;*.sqlite3;*.bak|All files|*.*", Title = "Seleccionar copia de seguridad para restaurar", InitialDirectory = backupDir };
                var res = dlg.ShowDialog();
                if (res != true) return;
                var path = dlg.FileName;

                var confirm = Services.DialogHelper.ConfirmYesNo("Restaurar copia", $"Restaurar desde {path}?\nSe conservará el estado anterior. La restauración requiere una ventana de mantenimiento, sin conexiones abiertas a la base de datos; si está en uso se rechazará la operación.");
                if (!confirm) return;

                var conn = Application.Current.Properties["AuditConnectionString"] as string;
                if (string.IsNullOrWhiteSpace(conn))
                {
                    var dbPath = System.IO.Path.Combine(ClinicaLongevidadApp.Services.AppPaths.BaseDir, "ClinicaLongevidad.db");
                    conn = $"Data Source={dbPath}";
                }

                if (_backupService is null)
                {
                    ShowSnackbar("Backup service not available", 5);
                    return;
                }

                _backupService.RestoreBackup(path, conn);
                ShowSnackbar("Restauración completada", 6);
                Services.DialogHelper.ShowInfo("Restauración", "Restauración completada. Reinicia la aplicación si es necesario.");
            }
            catch (Exception ex)
            {
                try { Services.DialogHelper.ShowError("Restauración", "Error restaurando copia: " + ex.Message); } catch { }
            }
        }

        public string DetalleRegistroFormateado
        {
            get
            {
                var r = RegistroSeleccionado;
                if (r is null) return string.Empty;

                var detalles = r.Detalles ?? string.Empty;
                try
                {
                    if (!string.IsNullOrWhiteSpace(detalles) && detalles.TrimStart().StartsWith("{"))
                    {
                        var node = JsonNode.Parse(detalles) as JsonObject ?? new JsonObject();

                        void Merge(string key, string? modelVal)
                        {
                            try
                            {
                                var has = node.ContainsKey(key);
                                string? jsonVal = null;
                                if (has)
                                {
                                    var jv = node[key];
                                    if (jv != null)
                                    {
                                        try { jsonVal = jv.GetValue<string>(); } catch { jsonVal = jv.ToString(); }
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(modelVal)) node[key] = modelVal;
                                else if (!has && !string.IsNullOrWhiteSpace(jsonVal)) node[key] = jsonVal;
                            }
                            catch { }
                        }

                        Merge("Rol", r.Rol);
                        Merge("Area", r.Area);
                        Merge("SesionId", r.SesionId);
                        Merge("Equipo", r.Equipo);
                        Merge("VersionApp", r.VersionApp);

                        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    }
                }
                catch { }

                try
                {
                    var obj = new JsonObject();
                    obj["Detalles"] = detalles;
                    if (!string.IsNullOrWhiteSpace(r.Rol)) obj["Rol"] = r.Rol;
                    if (!string.IsNullOrWhiteSpace(r.Area)) obj["Area"] = r.Area;
                    if (!string.IsNullOrWhiteSpace(r.SesionId)) obj["SesionId"] = r.SesionId;
                    if (!string.IsNullOrWhiteSpace(r.Equipo)) obj["Equipo"] = r.Equipo;
                    if (!string.IsNullOrWhiteSpace(r.VersionApp)) obj["VersionApp"] = r.VersionApp;
                    return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                }
                catch
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(detalles);
                    if (!string.IsNullOrWhiteSpace(r.Rol)) sb.AppendLine($"Rol: {r.Rol}");
                    if (!string.IsNullOrWhiteSpace(r.Area)) sb.AppendLine($"Area: {r.Area}");
                    if (!string.IsNullOrWhiteSpace(r.SesionId)) sb.AppendLine($"SesionId: {r.SesionId}");
                    if (!string.IsNullOrWhiteSpace(r.Equipo)) sb.AppendLine($"Equipo: {r.Equipo}");
                    if (!string.IsNullOrWhiteSpace(r.VersionApp)) sb.AppendLine($"VersionApp: {r.VersionApp}");
                    return sb.ToString();
                }
            }
        }

        public ICommand CopiarSesionIdCommand { get; private set; }
        public ICommand CopiarKeyVersionsCommand { get; private set; }
        public ICommand RotateHmacCommand { get; }
        public ICommand RotateEncCommand { get; }

        // Selected filters
        public string? UsuarioSeleccionado { get; set; }
        public string? ModuloSeleccionado { get; set; }
        public string? AccionSeleccionada { get; set; }
        public string? ResultadoSeleccionado { get; set; }
        public string? TipoSeleccionado { get; set; }
        public string? RolSeleccionado { get; set; }
        public string? AreaSeleccionada { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string TextoBusqueda { get; set; } = string.Empty;
        // Additional UI filter bindings present in XAML
        public bool MostrarSoloOperacionesHerramientas { get; set; }
        public System.Collections.ObjectModel.ObservableCollection<string> Severidades { get; } = new();
        public string? SeveridadSeleccionada { get; set; }
        public string? FiltroPacienteId { get; set; }
        public string? FiltroCitaId { get; set; }
        public string? FiltroSesionId { get; set; }

        // UI status / counters
        private string _mensajeError = string.Empty;
        public string MensajeError { get => _mensajeError; set { _mensajeError = value; OnPropertyChanged(nameof(MensajeError)); } }

        private int _registros;
        public int Registros { get => _registros; set { _registros = value; OnPropertyChanged(nameof(Registros)); } }

        private int _registrosOk;
        public int RegistrosOk { get => _registrosOk; set { _registrosOk = value; OnPropertyChanged(nameof(RegistrosOk)); } }

        private int _registrosError;
        public int RegistrosError { get => _registrosError; set { _registrosError = value; OnPropertyChanged(nameof(RegistrosError)); } }

        private int _tiposEnVista;
        public int TiposEnVista { get => _tiposEnVista; set { _tiposEnVista = value; OnPropertyChanged(nameof(TiposEnVista)); } }

        // Commands
        public ICommand ActualizarCommand { get; }
        public ICommand AplicarFiltrosCommand { get; }
        public ICommand LimpiarFiltrosCommand { get; }
        public ICommand ExportarCsvCommand { get; }

        public AuditoriaViewModelV2(AuditoriaService? auditoriaService = null)
        {
            _auditoriaService = auditoriaService;

            // try to create admin service from app properties
            try { var conn = Application.Current.Properties["AuditConnectionString"] as string; if (!string.IsNullOrWhiteSpace(conn)) _adminService = new AuditAdminService(conn); } catch { }

            // initialize sentinel values
            Usuarios.Add("-- Todos --");
            Modulos.Add("-- Todos --");
            Acciones.Add("-- Todos --");
            Resultados.Add("-- Todos --");
            Tipos.Add("-- Todos --");
            Roles.Add("-- Todos --");
            Areas.Add("-- Todos --");

            // Actualizar loads the complete history. The explicit short list is reserved
            // for the "Últimos movimientos" action.
            ActualizarCommand = new RelayCommand(async _ => await EjecutarActualizarAsync(limit: 0));
            AplicarFiltrosCommand = new RelayCommand(async _ => await AplicarFiltrosAsync());
            LimpiarFiltrosCommand = new RelayCommand(async _ => await LimpiarFiltrosAsync());
            ExportarCsvCommand = new RelayCommand(async _ => await ExportarCsvAsync());

            CopiarSesionIdCommand = new RelayCommand(_ => CopySesionId());
            CopiarKeyVersionsCommand = new RelayCommand(_ => CopyKeyVersions());
            RotateHmacCommand = new RelayCommand(async _ => await RotateHmacAsync());
            RotateEncCommand = new RelayCommand(async _ => await RotateEncAsync());
            // Backup commands
            BackupNowCommand = new RelayCommand(async _ => await BackupNowAsync());
            ScheduleBackupCommand = new RelayCommand(_ => ScheduleBackup());
            CancelBackupCommand = new RelayCommand(_ => CancelScheduledBackup());
            ScheduleOrCancelCommand = new RelayCommand(_ => ToggleSchedule());
            RestoreBackupCommand = new RelayCommand(async _ => await RestoreBackupAsync());
            TestScheduleInOneMinuteCommand = new RelayCommand(async _ => await ScheduleTestInOneMinuteAsync());
            CancelTestBackupCommand = new RelayCommand(_ => CancelTestBackup());
            ForceScheduledNowCommand = new RelayCommand(_ => ForceScheduledNow());

            _backupService = new ClinicaLongevidadApp.Services.BackupService();
            try { _backupService.BackupCompleted += OnBackupCompleted; } catch { }
            // restore persisted schedule if any
            try
            {
                var saved = LoadScheduledTime();
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    BackupTimeText = saved;
                    try { ScheduleBackup(); } catch { }
                }
            }
            catch { }
            try { Services.Sesion.SessionChanged += () => Application.Current?.Dispatcher?.Invoke(() => OnPropertyChanged(nameof(IsAdmin))); } catch { }
        }

        // Backup service and related commands/properties
        private readonly ClinicaLongevidadApp.Services.BackupService? _backupService;
        public ICommand? BackupNowCommand { get; private set; }
        public ICommand? ScheduleBackupCommand { get; private set; }
        public ICommand? CancelBackupCommand { get; private set; }
        public ICommand? ScheduleOrCancelCommand { get; private set; }
        public ICommand? RestoreBackupCommand { get; private set; }
        private string? _backupTimeText = string.Empty;
        private string? _savedBackupTime = null;
        public string BackupTimeText
        {
            get => _backupTimeText ?? string.Empty;
            set
            {
                var newVal = value ?? string.Empty;
                if (string.Equals(_backupTimeText, newVal, StringComparison.Ordinal)) return;
                _backupTimeText = newVal;
                // If user changes the time away from the saved scheduled time, mark as not scheduled
                try
                {
                    if (!string.IsNullOrWhiteSpace(_savedBackupTime) && !string.Equals(_savedBackupTime, _backupTimeText, StringComparison.Ordinal))
                    {
                        IsBackupScheduled = false;
                    }
                }
                catch { }
                OnPropertyChanged(nameof(BackupTimeText));
                OnPropertyChanged(nameof(ScheduleButtonText));
            }
        } // e.g. "23:30"

        public ICommand? TestScheduleInOneMinuteCommand { get; private set; }
        public ICommand? CancelTestBackupCommand { get; private set; }
        public ICommand? ForceScheduledNowCommand { get; private set; }

        private System.Threading.CancellationTokenSource? _testBackupCancellation;
        private bool _isTestBackupScheduled;
        public bool IsTestBackupScheduled
        {
            get => _isTestBackupScheduled;
            private set
            {
                _isTestBackupScheduled = value;
                OnPropertyChanged(nameof(IsTestBackupScheduled));
            }
        }

        private DateTime? _nextScheduledRun;
        public DateTime? NextScheduledRun { get => _nextScheduledRun; private set { _nextScheduledRun = value; OnPropertyChanged(nameof(NextScheduledRun)); OnPropertyChanged(nameof(NextScheduledRunText)); } }

        public string NextScheduledRunText => NextScheduledRun.HasValue ? NextScheduledRun.Value.ToString("dd/MM/yyyy HH:mm") : "-- Ninguna --";

        private string _backupScheduleStatus = "No hay copia programada.";
        public string BackupScheduleStatus
        {
            get => _backupScheduleStatus;
            private set { _backupScheduleStatus = value; OnPropertyChanged(nameof(BackupScheduleStatus)); }
        }

        // Exposed for XAML visibility bindings
        public bool IsAdmin
        {
            get
            {
                try
                {
                    var role = Services.Sesion.RolActual ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(role)) return false;
                    role = role.Trim();
                    if (role.IndexOf("admin", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (string.Equals(role, "administración", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "administracion", StringComparison.OrdinalIgnoreCase)) return true;
                    return false;
                }
                catch { return false; }
            }
        }

        private bool _isBackupScheduled;
        public bool IsBackupScheduled { get => _isBackupScheduled; private set { _isBackupScheduled = value; OnPropertyChanged(nameof(IsBackupScheduled)); OnPropertyChanged(nameof(ScheduleButtonText)); } }

        public string ScheduleButtonText => IsBackupScheduled ? "Cancelar" : "Programar";

        public void Cleanup()
        {
            try
            {
                try { if (_backupService != null) _backupService.BackupCompleted -= OnBackupCompleted; } catch { }
                try { _backupService?.CancelScheduledBackup(); } catch { }
                try { _testBackupCancellation?.Cancel(); } catch { }
            }
            catch { }
        }

        private DispatcherTimer? _snackTimer;
        private bool _snackbarVisible;
        public bool SnackbarVisible
        {
            get => _snackbarVisible;
            set { _snackbarVisible = value; OnPropertyChanged(nameof(SnackbarVisible)); }
        }

        private async System.Threading.Tasks.Task RotateHmacAsync()
        {
            try
            {
                var confirm = MessageBox.Show("¿Confirma rotación de clave HMAC? Se persistirá en el almacenamiento de claves configurado.", "Rotar HMAC", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                var executor = ClinicaLongevidadApp.Services.KeyRotation.KeyRotationProviderFactory.Create(_auditoriaService);
                // generate 32-byte key for HMAC-SHA256
                var key = ClinicaLongevidadApp.Services.KeyRotation.KeyRotationService.GenerateRandomKey(32);
                var newVersion = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                var plan = await executor.ApplyRotateHmacAsync(key, newVersion);

                try
                {
                    if (_auditoriaService == null)
                    {
                        throw new InvalidOperationException("El servicio de auditoría no está disponible.");
                    }

                    _auditoriaService.RegistrarEvento(new Models.AuditoriaEvento
                    {
                        UsuarioAdmin = Sesion.UsuarioActual ?? "Sistema",
                        Accion = "Rotate.HMAC",
                        Modulo = "RotateKeys",
                        UsuarioAfectado = Sesion.UsuarioActual ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.Now,
                        Tipo = "Operación",
                        Detalles = Services.AuditoriaDetallesHelper.CrearJson(("OldVersion", plan.OldVersion ?? string.Empty), ("NewVersion", plan.NewVersion ?? string.Empty), ("Notes", plan.Notes ?? string.Empty))
                    });
                }
                catch (Exception auditException)
                {
                    MessageBox.Show($"La clave HMAC se ha persistido con versión {plan.NewVersion}, pero no se pudo registrar la auditoría. Revise la incidencia antes de continuar: {auditException.Message}", "Auditoría pendiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // update UI: set key version on selected record (conservative UI feedback)
                try
                {
                    if (RegistroSeleccionado != null)
                    {
                        RegistroSeleccionado.KeyVersion = plan.NewVersion ?? RegistroSeleccionado.KeyVersion;
                        OnPropertyChanged(nameof(RegistroSeleccionado));
                        OnPropertyChanged(nameof(DetalleRegistroFormateado));
                    }
                }
                catch { }

                ShowSnackbar($"Rotación HMAC aplicada: {plan.NewVersion}", 6);

                // refresh list so new audit event becomes visible
                try
                {
                    var selId = RegistroSeleccionado?.Id;
                    await EjecutarActualizarAsync();
                    if (selId != null)
                    {
                        try
                        {
                            var match = ListaAuditoria.FirstOrDefault(x => x.Id == selId);
                            if (match != null)
                            {
                                RegistroSeleccionado = match;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                try { MessageBox.Show("Rotación HMAC fallida: " + ex.Message, "Rotar HMAC", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }

        private async System.Threading.Tasks.Task RotateEncAsync()
        {
            try
            {
                var confirm = MessageBox.Show("¿Confirma rotación de clave de encriptación? Se persistirá en el almacenamiento de claves configurado.", "Rotar ENC", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                var executor = ClinicaLongevidadApp.Services.KeyRotation.KeyRotationProviderFactory.Create(_auditoriaService);
                // generate 32-byte key for AES-GCM
                var key = ClinicaLongevidadApp.Services.KeyRotation.KeyRotationService.GenerateRandomKey(32);
                var newVersion = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                var plan = await executor.ApplyRotateEncAsync(key, newVersion);

                try
                {
                    if (_auditoriaService == null)
                    {
                        throw new InvalidOperationException("El servicio de auditoría no está disponible.");
                    }

                    _auditoriaService.RegistrarEvento(new Models.AuditoriaEvento
                    {
                        UsuarioAdmin = Sesion.UsuarioActual ?? "Sistema",
                        Accion = "Rotate.ENC",
                        Modulo = "RotateKeys",
                        UsuarioAfectado = Sesion.UsuarioActual ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.Now,
                        Tipo = "Operación",
                        Detalles = Services.AuditoriaDetallesHelper.CrearJson(("OldVersion", plan.OldVersion ?? string.Empty), ("NewVersion", plan.NewVersion ?? string.Empty), ("Notes", plan.Notes ?? string.Empty))
                    });
                }
                catch (Exception auditException)
                {
                    MessageBox.Show($"La clave de cifrado se ha persistido con versión {plan.NewVersion}, pero no se pudo registrar la auditoría. Revise la incidencia antes de continuar: {auditException.Message}", "Auditoría pendiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    if (RegistroSeleccionado != null)
                    {
                        RegistroSeleccionado.KeyVersionEnc = plan.NewVersion ?? RegistroSeleccionado.KeyVersionEnc;
                        OnPropertyChanged(nameof(RegistroSeleccionado));
                        OnPropertyChanged(nameof(DetalleRegistroFormateado));
                    }
                }
                catch { }

                ShowSnackbar($"Rotación ENC aplicada: {plan.NewVersion}", 6);

                try
                {
                    var selId = RegistroSeleccionado?.Id;
                    await EjecutarActualizarAsync();
                    if (selId != null)
                    {
                        try
                        {
                            var match = ListaAuditoria.FirstOrDefault(x => x.Id == selId);
                            if (match != null)
                            {
                                RegistroSeleccionado = match;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                try { MessageBox.Show("Rotación ENC fallida: " + ex.Message, "Rotar ENC", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }

        private string _snackbarMessage = string.Empty;
        public string SnackbarMessage
        {
            get => _snackbarMessage;
            set { _snackbarMessage = value; OnPropertyChanged(nameof(SnackbarMessage)); }
        }

        private void ShowSnackbar(string message, int seconds = 3)
        {
            try
            {
                SnackbarMessage = message;
                SnackbarVisible = true;
                if (_snackTimer == null)
                {
                    _snackTimer = new DispatcherTimer();
                    _snackTimer.Tick += (s, e) =>
                    {
                        SnackbarVisible = false;
                        _snackTimer?.Stop();
                    };
                }
                _snackTimer.Interval = TimeSpan.FromSeconds(seconds);
                _snackTimer.Start();
            }
            catch { }
        }

        private void OnBackupCompleted(string backupPath)
        {
            try
            {
                // invoked from background timer thread - marshal to UI thread
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try { Services.AuditLogHelper.Info("Auditoría.UI", $"Scheduled backup completed: {backupPath}"); } catch { }
                    try { ShowSnackbar($"Copia programada creada en: {backupPath}", 6); } catch { }
                    // update next scheduled run (recurring daily)
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(BackupTimeText) && TimeSpan.TryParse(BackupTimeText, out var ts))
                        {
                            var now = DateTime.Now;
                            var next = new DateTime(now.Year, now.Month, now.Day, ts.Hours, ts.Minutes, 0).AddDays(1);
                            NextScheduledRun = next;
                        }
                        else
                        {
                            NextScheduledRun = null;
                        }
                        BackupScheduleStatus = $"Última copia creada en: {backupPath}.";
                    }
                    catch { }
                });
            }
            catch { }
        }

        public async Task CargarUltimosAsync(int limit)
        {
            await EjecutarActualizarAsync(limit);
            ShowSnackbar($"Mostrando los últimos {ListaAuditoria.Count} registros.", 4);
        }

        // A limit <= 0 means complete history; AuditoriaService follows the same contract.
        public async Task EjecutarActualizarAsync(int limit = 0)
        {
            try
            {
                ListaAuditoria.Clear();

                List<AuditoriaModel>? rows = null;
                if (_auditoriaService is not null)
                {
                    rows = await Task.Run(() => _auditoriaService.GetRecentAudits(limit));
                }
                else if (_adminService is not null)
                {
                    var temp = await Task.Run(() => _adminService.GetRecentAudits(limit));
                    rows = temp.Select(r => new AuditoriaModel
                    {
                        Id = r.Id,
                        FechaHora = DateTime.TryParse(r.FechaHora, out var dt) ? dt : DateTime.MinValue,
                        UsuarioAdmin = r.UsuarioAdmin ?? string.Empty,
                        Accion = r.Accion ?? string.Empty,
                        Modulo = r.Modulo ?? string.Empty,
                        UsuarioAfectado = r.UsuarioAfectado ?? string.Empty,
                        Resultado = r.Resultado ?? string.Empty,
                        Tipo = r.Tipo ?? string.Empty,
                        Rol = r.Rol ?? string.Empty,
                        Area = r.Area ?? string.Empty,
                        SesionId = r.SesionId ?? string.Empty,
                        Equipo = r.Equipo ?? string.Empty,
                        VersionApp = r.VersionApp ?? string.Empty,
                        Detalles = r.DetallesPlain ?? r.Detalles ?? string.Empty,
                        KeyVersion = r.KeyVersion ?? string.Empty,
                        KeyVersionEnc = r.KeyVersionEnc ?? string.Empty
                    }).ToList();
                }

                if (rows is null) rows = new List<AuditoriaModel>();

                // try to extract metadata from Detalles JSON when missing
                foreach (var m in rows)
                {
                    TryPopulateMetadataFromDetalles(m);
                }

                // Ensure rows are ordered by FechaHora DESC so UI shows newest first
                rows = rows.OrderByDescending(r => r.FechaHora).ToList();
                // cache full dataset
                _rowsAll = rows.ToList();

                // Populate UI collection
                foreach (var r in rows)
                    ListaAuditoria.Add(r);

                // update counters/metrics
                UpdateMetrics(rows);
                ShowSnackbar($"Auditoría actualizada: {rows.Count} registros cargados.", 4);

                // Ensure UI view is sorted by FechaHora DESC so newest records appear first
                try
                {
                    var cv = System.Windows.Data.CollectionViewSource.GetDefaultView(ListaAuditoria);
                    if (cv != null)
                    {
                        cv.SortDescriptions.Clear();
                        cv.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(Models.AuditoriaModel.FechaHora), System.ComponentModel.ListSortDirection.Descending));
                        cv.Refresh();
                    }
                }
                catch { }

                // Populate filter lists from superset
                try { Application.Current?.Dispatcher?.Invoke(() => PopulateFilterCollectionsInPlace(_rowsAll)); } catch { }
            }
            catch (Exception ex)
            {
                try { Services.DialogHelper.ShowError("Auditoría - Error", ex.Message); } catch { }
            }
        }

        public async Task AplicarFiltrosAsync()
        {
            try
            {
                var query = _rowsAll.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(TextoBusqueda))
                {
                    var t = TextoBusqueda.Trim();
                    query = query.Where(r => (
                        (r.UsuarioAdmin ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (r.Accion ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (r.Modulo ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (r.Resultado ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (r.UsuarioAfectado ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (r.Detalles ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase)
                    ));
                }

                if (!string.IsNullOrWhiteSpace(UsuarioSeleccionado) && UsuarioSeleccionado != "-- Todos --")
                    query = query.Where(r => string.Equals(r.UsuarioAdmin?.Trim(), UsuarioSeleccionado.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(ModuloSeleccionado) && ModuloSeleccionado != "-- Todos --")
                    query = query.Where(r => string.Equals(r.Modulo?.Trim(), ModuloSeleccionado.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(AccionSeleccionada) && AccionSeleccionada != "-- Todos --")
                    query = query.Where(r => string.Equals(r.Accion?.Trim(), AccionSeleccionada.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(ResultadoSeleccionado) && ResultadoSeleccionado != "-- Todos --")
                    query = query.Where(r => string.Equals(r.Resultado?.Trim(), ResultadoSeleccionado.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(TipoSeleccionado) && TipoSeleccionado != "-- Todos --")
                    query = query.Where(r => string.Equals(r.Tipo?.Trim(), TipoSeleccionado.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(RolSeleccionado) && RolSeleccionado != "-- Todos --")
                    query = query.Where(r => string.Equals(r.Rol?.Trim(), RolSeleccionado.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(AreaSeleccionada) && AreaSeleccionada != "-- Todos --")
                    query = query.Where(r => string.Equals(r.Area?.Trim(), AreaSeleccionada.Trim(), StringComparison.OrdinalIgnoreCase));

                if (FechaDesde.HasValue)
                    query = query.Where(r => r.FechaHora >= FechaDesde.Value.Date);
                if (FechaHasta.HasValue)
                    query = query.Where(r => r.FechaHora <= FechaHasta.Value.Date.AddDays(1).AddTicks(-1));

                ListaAuditoria.Clear();
                var filtered = query.ToList();
                foreach (var m in filtered) ListaAuditoria.Add(m);

                // update counters
                UpdateMetrics(filtered);

                // Ensure UI view preserves FechaHora DESC after applying filters
                try
                {
                    var cv = System.Windows.Data.CollectionViewSource.GetDefaultView(ListaAuditoria);
                    if (cv != null)
                    {
                        cv.SortDescriptions.Clear();
                        cv.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(Models.AuditoriaModel.FechaHora), System.ComponentModel.ListSortDirection.Descending));
                        cv.Refresh();
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                try { Services.DialogHelper.ShowError("Aplicar filtros", ex.Message); } catch { }
            }
            await Task.CompletedTask;
        }

        public Task LimpiarFiltrosAsync()
        {
            TextoBusqueda = string.Empty;
            UsuarioSeleccionado = "-- Todos --";
            ModuloSeleccionado = "-- Todos --";
            AccionSeleccionada = "-- Todos --";
            ResultadoSeleccionado = "-- Todos --";
            TipoSeleccionado = "-- Todos --";
            RolSeleccionado = "-- Todos --";
            AreaSeleccionada = "-- Todos --";
            FechaDesde = null;
            FechaHasta = null;
            MostrarSoloOperacionesHerramientas = false;
            SeveridadSeleccionada = null;
            FiltroPacienteId = null;
            FiltroCitaId = null;
            FiltroSesionId = null;

            // These filter properties are auto-properties. Notify their bindings explicitly
            // so the controls visibly reset as well as the query state.
            OnPropertyChanged(nameof(TextoBusqueda));
            OnPropertyChanged(nameof(UsuarioSeleccionado));
            OnPropertyChanged(nameof(ModuloSeleccionado));
            OnPropertyChanged(nameof(AccionSeleccionada));
            OnPropertyChanged(nameof(ResultadoSeleccionado));
            OnPropertyChanged(nameof(TipoSeleccionado));
            OnPropertyChanged(nameof(RolSeleccionado));
            OnPropertyChanged(nameof(AreaSeleccionada));
            OnPropertyChanged(nameof(FechaDesde));
            OnPropertyChanged(nameof(FechaHasta));
            OnPropertyChanged(nameof(MostrarSoloOperacionesHerramientas));
            OnPropertyChanged(nameof(SeveridadSeleccionada));
            OnPropertyChanged(nameof(FiltroPacienteId));
            OnPropertyChanged(nameof(FiltroCitaId));
            OnPropertyChanged(nameof(FiltroSesionId));

            // restore full dataset
            ListaAuditoria.Clear();
            foreach (var r in _rowsAll) ListaAuditoria.Add(r);

            UpdateMetrics(_rowsAll);
            // Ensure UI view preserves FechaHora DESC after clearing filters
            try
            {
                var cv = System.Windows.Data.CollectionViewSource.GetDefaultView(ListaAuditoria);
                if (cv != null)
                {
                    cv.SortDescriptions.Clear();
                    cv.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(Models.AuditoriaModel.FechaHora), System.ComponentModel.ListSortDirection.Descending));
                    cv.Refresh();
                }
            }
            catch { }
            ShowSnackbar($"Filtros limpiados. Mostrando {_rowsAll.Count} registros.", 4);
            return Task.CompletedTask;
        }

        private void PopulateFilterCollectionsInPlace(IEnumerable<AuditoriaModel> rows)
        {
            var list = rows?.ToList() ?? new List<AuditoriaModel>();

                void Update(ObservableCollection<string> target, IEnumerable<string?> values)
            {
                var vals = values.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => (s ?? string.Empty).Trim())
                                 .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
                var desired = new List<string>(1 + vals.Count) { "-- Todos --" };
                desired.AddRange(vals);

                for (int i = target.Count - 1; i >= 0; i--)
                {
                    var item = target[i];
                    if (!desired.Any(d => string.Equals(d, item, StringComparison.OrdinalIgnoreCase))) target.RemoveAt(i);
                }
                for (int i = 0; i < desired.Count; i++)
                {
                    var d = desired[i];
                    var existing = target.FirstOrDefault(x => string.Equals(x?.Trim(), d, StringComparison.OrdinalIgnoreCase));
                    var idx = existing == null ? -1 : target.IndexOf(existing);
                    if (idx == -1) target.Insert(i, d);
                    else if (idx != i) try { target.Move(idx, i); } catch { }
                }
            }

            Update(Usuarios, list.Select(r => r.UsuarioAdmin));
            Update(Modulos, list.Select(r => r.Modulo));
            Update(Acciones, list.Select(r => r.Accion));
            Update(Resultados, list.Select(r => r.Resultado));
            Update(Tipos, list.Select(r => r.Tipo));
            Update(Roles, list.Select(r => r.Rol));
            Update(Areas, list.Select(r => r.Area));
        }

        private void TryPopulateMetadataFromDetalles(AuditoriaModel m)
        {
            try
            {
                if (m == null) return;
                var detalles = m.Detalles ?? string.Empty;
                if (string.IsNullOrWhiteSpace(detalles)) return;
                if (!detalles.TrimStart().StartsWith("{")) return;

                using var doc = JsonDocument.Parse(detalles);
                var root = doc.RootElement;
                static bool TryGetAny(JsonElement rootElement, string[] names, out string? value)
                {
                    value = null;
                    foreach (var n in names)
                    {
                        if (rootElement.TryGetProperty(n, out var pj) && pj.ValueKind == JsonValueKind.String)
                        {
                            value = pj.GetString();
                            if (!string.IsNullOrWhiteSpace(value)) return true;
                        }
                    }
                    // case-insensitive search through properties
                    foreach (var prop in rootElement.EnumerateObject())
                    {
                        foreach (var n in names)
                        {
                            if (string.Equals(prop.Name, n, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.String)
                            {
                                value = prop.Value.GetString();
                                if (!string.IsNullOrWhiteSpace(value)) return true;
                            }
                        }
                    }
                    return false;
                }

                // aliases for common keys
                if (string.IsNullOrWhiteSpace(m.Rol))
                {
                    if (TryGetAny(root, new[] { "Rol", "rol", "Role", "role", "Area", "area" }, out var v)) m.Rol = v ?? m.Rol;
                }
                if (string.IsNullOrWhiteSpace(m.Area))
                {
                    if (TryGetAny(root, new[] { "Area", "area", "Rol", "rol" }, out var v)) m.Area = v ?? m.Area;
                }
                if (string.IsNullOrWhiteSpace(m.SesionId))
                {
                    if (TryGetAny(root, new[] { "SesionId", "Sesion", "sessionId", "session", "SessionId", "session_id" }, out var v)) m.SesionId = v ?? m.SesionId;
                }
                if (string.IsNullOrWhiteSpace(m.Equipo))
                {
                    if (TryGetAny(root, new[] { "Equipo", "Host", "Machine", "HostName", "host", "machine" }, out var v)) m.Equipo = v ?? m.Equipo;
                }
                if (string.IsNullOrWhiteSpace(m.VersionApp))
                {
                    if (TryGetAny(root, new[] { "VersionApp", "AppVersion", "version", "appVersion" }, out var v)) m.VersionApp = v ?? m.VersionApp;
                }

                // Normalize fallback: if Rol is empty but Area has a value, prefer Area as Rol for display
                if (string.IsNullOrWhiteSpace(m.Rol) && !string.IsNullOrWhiteSpace(m.Area))
                {
                    m.Rol = m.Area;
                }

                // If still empty, set marker
                if (string.IsNullOrWhiteSpace(m.Rol)) m.Rol = "N/D";
                if (string.IsNullOrWhiteSpace(m.Area)) m.Area = "N/D";
                if (string.IsNullOrWhiteSpace(m.SesionId)) m.SesionId = "N/D";
                if (string.IsNullOrWhiteSpace(m.Equipo)) m.Equipo = "N/D";
                if (string.IsNullOrWhiteSpace(m.VersionApp)) m.VersionApp = "N/D";
            }
            catch { }
        }

        private void CopySesionId()
        {
            try
            {
                var text = RegistroSeleccionado?.SesionId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text)) return;
                Application.Current.Dispatcher.Invoke(() => System.Windows.Clipboard.SetText(text));
                ShowSnackbar("SesionId copiado al portapapeles");
            }
            catch { }
        }

        private void CopyKeyVersions()
        {
            try
            {
                var kv = (RegistroSeleccionado?.KeyVersion ?? string.Empty) + " / " + (RegistroSeleccionado?.KeyVersionEnc ?? string.Empty);
                if (string.IsNullOrWhiteSpace(kv)) return;
                Application.Current.Dispatcher.Invoke(() => System.Windows.Clipboard.SetText(kv));
                ShowSnackbar("Versiones de clave copiadas al portapapeles");
            }
            catch { }
        }

        private async Task ExportarCsvAsync()
        {
            try
            {
                if (ListaAuditoria == null || ListaAuditoria.Count == 0)
                {
                    MessageBox.Show("No hay registros para exportar.", "Exportar CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var defaultName = $"AuditExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var dlg = new Microsoft.Win32.SaveFileDialog() { FileName = defaultName, DefaultExt = ".csv", Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*", InitialDirectory = ClinicaLongevidadApp.Services.AppPaths.ExportsDir };
                var res = dlg.ShowDialog();
                if (res != true) return;
                var path = dlg.FileName;

                var sb = new StringBuilder();
                sb.AppendLine("Id;FechaHora;UsuarioAdmin;Accion;Modulo;UsuarioAfectado;Resultado;Tipo;Rol;Area;SesionId;Equipo;VersionApp;KeyVersion;KeyVersionEnc;Detalles");
                foreach (var r in ListaAuditoria)
                {
                    string Escape(string? s) { if (s == null) return string.Empty; var v = s.Replace("\"", "\"\""); if (v.Contains(';') || v.Contains('"') || v.Contains('\n') || v.Contains('\r')) return '"' + v + '"'; return v; }
                    var line = string.Join(";", new string[] { r.Id.ToString(), r.FechaHora == DateTime.MinValue ? string.Empty : r.FechaHora.ToString("dd/MM/yyyy HH:mm:ss"), Escape(r.UsuarioAdmin), Escape(r.Accion), Escape(r.Modulo), Escape(r.UsuarioAfectado), Escape(r.Resultado), Escape(r.Tipo), Escape(r.Rol), Escape(r.Area), Escape(r.SesionId), Escape(r.Equipo), Escape(r.VersionApp), Escape(r.KeyVersion), Escape(r.KeyVersionEnc), Escape(r.Detalles) });
                    sb.AppendLine(line);
                }
                System.IO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Exportación CSV completada:\n{path}", "Exportar CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
            await Task.CompletedTask;
        }
    }
}
