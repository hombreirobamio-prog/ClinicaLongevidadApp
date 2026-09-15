using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System;
using System.Globalization;
using ClinicaLongevidadApp.Commands;
using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Win32;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        private void DumpAdminDebugCsv(IEnumerable<AuditAdminService.AuditRecentDto> rows)
        {
            try
            {
                var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicaLongevidadApp", "logs");
                Directory.CreateDirectory(baseDir);
                var file = Path.Combine(baseDir, $"audit_debug_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                string Escape(string? s)
                {
                    if (s == null) return string.Empty;
                    var v = s.Replace("\"", "\"\"");
                    if (v.Contains(';') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
                        return '"' + v + '"';
                    return v;
                }

                var sb = new StringBuilder();
                sb.AppendLine("Id;EventId;FechaHora;UsuarioAdmin;Accion;Modulo;UsuarioAfectado;Resultado;Detalles;DetallesPlain;DetallesEnc;Tipo;Rol;Area;SesionId;Equipo;VersionApp;KeyVersion;KeyVersionEnc");
                foreach (var r in rows)
                {
                    var line = string.Join(";", new string[] {
                        (r.Id).ToString(),
                        Escape(r.EventId),
                        Escape(r.FechaHora),
                        Escape(r.UsuarioAdmin),
                        Escape(r.Accion),
                        Escape(r.Modulo),
                        Escape(r.UsuarioAfectado),
                        Escape(r.Resultado),
                        Escape(r.Detalles),
                        Escape(r.DetallesPlain),
                        Escape(r.DetallesEnc),
                        Escape(r.Tipo),
                        Escape(r.Rol),
                        Escape(r.Area),
                        Escape(r.SesionId),
                        Escape(r.Equipo),
                        Escape(r.VersionApp),
                        Escape(r.KeyVersion),
                        Escape(r.KeyVersionEnc)
                    });
                    sb.AppendLine(line);
                }

                File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
                ShowSnackbar($"Audit debug written: {file}", 5);
            }
            catch { }
        }

        private static bool IsBase64(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            try
            {
                Span<byte> buffer = new byte[s.Length];
                return Convert.TryFromBase64String(s, buffer, out _);
            }
            catch { return false; }
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

        private async System.Threading.Tasks.Task ExportarCsvAsync()
        {
            try
            {
                if (ListaAuditoria == null || ListaAuditoria.Count == 0)
                {
                    MessageBox.Show("No hay registros para exportar.", "Exportar CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var defaultName = $"AuditExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var dlg = new Microsoft.Win32.SaveFileDialog()
                {
                    FileName = defaultName,
                    DefaultExt = ".csv",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                var res = dlg.ShowDialog();
                if (res != true) return;

                var path = dlg.FileName;
                var sb = new StringBuilder();
                // Header
                sb.AppendLine("Id;FechaHora;UsuarioAdmin;Accion;Modulo;UsuarioAfectado;Resultado;Tipo;Rol;Area;SesionId;Equipo;VersionApp;KeyVersion;KeyVersionEnc;Detalles");

                foreach (var r in ListaAuditoria)
                {
                    string Escape(string? s)
                    {
                        if (s == null) return string.Empty;
                        var v = s.Replace("\"", "\"\"");
                        if (v.Contains(';') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
                            return '"' + v + '"';
                        return v;
                    }

                    var line = string.Join(";", new string[] {
                        r.Id.ToString(),
                        r.FechaHora == DateTime.MinValue ? string.Empty : r.FechaHora.ToString("dd/MM/yyyy HH:mm:ss"),
                        Escape(r.UsuarioAdmin),
                        Escape(r.Accion),
                        Escape(r.Modulo),
                        Escape(r.UsuarioAfectado),
                        Escape(r.Resultado),
                        Escape(r.Tipo),
                        Escape(r.Rol),
                        Escape(r.Area),
                        Escape(r.SesionId),
                        Escape(r.Equipo),
                        Escape(r.VersionApp),
                        Escape(r.KeyVersion),
                        Escape(r.KeyVersionEnc),
                        Escape(r.Detalles)
                    });
                    sb.AppendLine(line);
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Exportación CSV completada:\n{path}", "Exportar CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                try { MessageBox.Show("Error exportando CSV: " + ex.Message, "Exportar CSV", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }

        // Clipboard / snackbar helpers
        public ICommand CopiarSesionIdCommand { get; }
        public ICommand CopiarKeyVersionsCommand { get; }

        private bool _snackbarVisible;
        public bool SnackbarVisible
        {
            get => _snackbarVisible;
            set { _snackbarVisible = value; OnPropertyChanged(nameof(SnackbarVisible)); }
        }

        private string _snackbarMessage = string.Empty;
        public string SnackbarMessage
        {
            get => _snackbarMessage;
            set { _snackbarMessage = value; OnPropertyChanged(nameof(SnackbarMessage)); }
        }

        private DispatcherTimer? _snackTimer;


        public async System.Threading.Tasks.Task CargarUltimosAsync(int limit)
        {
            try
            {
                ListaAuditoria.Clear();

                List<Models.AuditoriaModel> rows = null;
                if (_auditoriaService is not null)
                {
                    rows = await System.Threading.Tasks.Task.Run(() => _auditoriaService.GetRecentAudits(limit));
                }
                else if (_adminService is not null)
                {
                    var temp = await System.Threading.Tasks.Task.Run(() => _adminService.GetRecentAudits(limit));
                    try { DumpAdminDebugCsv(temp); } catch { }
                    var kp = new LocalKeyProvider();
                    var list = new List<Models.AuditoriaModel>(temp.Count);
                    foreach (var r in temp)
                    {
                        var m = new Models.AuditoriaModel
                        {
                            Id = r.Id,
                            EventId = r.EventId,
                            FechaHora = ParseFecha(r.FechaHora),
                            UsuarioAdmin = r.UsuarioAdmin,
                            Accion = r.Accion,
                            Modulo = r.Modulo,
                            UsuarioAfectado = r.UsuarioAfectado,
                            Resultado = r.Resultado,
                            Tipo = r.Tipo ?? string.Empty,
                            Rol = r.Rol ?? string.Empty,
                            Area = r.Area ?? string.Empty,
                            SesionId = r.SesionId ?? string.Empty,
                            Equipo = r.Equipo ?? string.Empty,
                            VersionApp = r.VersionApp ?? string.Empty,
                            KeyVersion = r.KeyVersion ?? string.Empty,
                            KeyVersionEnc = r.KeyVersionEnc ?? string.Empty
                        };

                        // Prefer explicit plain column
                        var detallesPlain = string.IsNullOrWhiteSpace(r.DetallesPlain) ? string.Empty : r.DetallesPlain;
                        var detallesLegacy = string.IsNullOrWhiteSpace(r.Detalles) ? string.Empty : r.Detalles;
                        var detallesEnc = string.IsNullOrWhiteSpace(r.DetallesEnc) ? string.Empty : r.DetallesEnc;

                        string finalDetalles = detallesPlain;

                        if (string.IsNullOrWhiteSpace(finalDetalles))
                        {
                            // if legacy Detalles contains readable text (not base64), use it
                            if (!string.IsNullOrWhiteSpace(detallesLegacy) && !IsBase64(detallesLegacy))
                            {
                                finalDetalles = detallesLegacy;
                            }
                            else if (!string.IsNullOrWhiteSpace(detallesEnc) || IsBase64(detallesLegacy))
                            {
                                var blob = !string.IsNullOrWhiteSpace(detallesEnc) ? detallesEnc : detallesLegacy;
                                try
                                {
                                    var combined = Convert.FromBase64String(blob);
                                    // Try to obtain encryption key by version, fall back to current provider key
                                    byte[]? encKey = null;
                                    try { encKey = kp.GetEncryptionKeyByVersion(r.KeyVersionEnc); } catch { encKey = null; }
                                    encKey ??= kp.GetEncryptionKey();

                                    if (encKey != null && encKey.Length > 0)
                                    {
                                        // Expect: nonce(12) | tag(16) | cipher
                                        if (combined.Length > 28)
                                        {
                                            var nonce = new byte[12];
                                            var tag = new byte[16];
                                            var cipher = new byte[combined.Length - nonce.Length - tag.Length];
                                            Buffer.BlockCopy(combined, 0, nonce, 0, nonce.Length);
                                            Buffer.BlockCopy(combined, nonce.Length, tag, 0, tag.Length);
                                            Buffer.BlockCopy(combined, nonce.Length + tag.Length, cipher, 0, cipher.Length);
                                            var plain = new byte[cipher.Length];
                                            try
                                            {
                                                using var aesg = new System.Security.Cryptography.AesGcm(encKey);
                                                aesg.Decrypt(nonce, cipher, tag, plain);
                                                finalDetalles = Encoding.UTF8.GetString(plain);
                                            }
                                            catch
                                            {
                                                // decryption failed - fallback to blob text
                                                finalDetalles = blob;
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    finalDetalles = blob; // fallback
                                }
                            }
                        }

                        m.Detalles = finalDetalles ?? string.Empty;

                        // If metadata fields are still empty, try to extract from the (possibly decrypted) Detalles JSON
                        try
                        {
                            if ((!string.IsNullOrWhiteSpace(m.Detalles) && m.Detalles.TrimStart().StartsWith("{")) &&
                                (string.IsNullOrWhiteSpace(m.Rol) || string.IsNullOrWhiteSpace(m.Area) || string.IsNullOrWhiteSpace(m.SesionId) || string.IsNullOrWhiteSpace(m.Equipo) || string.IsNullOrWhiteSpace(m.VersionApp)))
                            {
                                using var doc = JsonDocument.Parse(m.Detalles);
                                var root = doc.RootElement;
                                if (string.IsNullOrWhiteSpace(m.Rol) && root.TryGetProperty("Rol", out var pRol) && pRol.ValueKind == JsonValueKind.String)
                                    m.Rol = pRol.GetString() ?? m.Rol;
                                if (string.IsNullOrWhiteSpace(m.Area) && root.TryGetProperty("Area", out var pArea) && pArea.ValueKind == JsonValueKind.String)
                                    m.Area = pArea.GetString() ?? m.Area;
                                if (string.IsNullOrWhiteSpace(m.SesionId) && root.TryGetProperty("SesionId", out var pSes) && pSes.ValueKind == JsonValueKind.String)
                                    m.SesionId = pSes.GetString() ?? m.SesionId;
                                if (string.IsNullOrWhiteSpace(m.Equipo) && root.TryGetProperty("Equipo", out var pEq) && pEq.ValueKind == JsonValueKind.String)
                                    m.Equipo = pEq.GetString() ?? m.Equipo;
                                if (string.IsNullOrWhiteSpace(m.VersionApp) && root.TryGetProperty("VersionApp", out var pVer) && pVer.ValueKind == JsonValueKind.String)
                                    m.VersionApp = pVer.GetString() ?? m.VersionApp;
                            }
                        }
                        catch { }
                        list.Add(m);
                    }

                    rows = list;
                }

                if (rows != null)
                {
                    // Normalize/fallbacks: ensure UI-visible metadata is populated when possible
                    foreach (var model in rows)
                    {
                        // If Rol is empty but Area has a sensible value, prefer Area as Rol for display
                        try
                        {
                            if (string.IsNullOrWhiteSpace(model.Rol) && !string.IsNullOrWhiteSpace(model.Area))
                            {
                                model.Rol = model.Area;
                            }
                        }
                        catch { }

                        ListaAuditoria.Add(model);
                    }
                }

                // Update computed counters and visible types
                UpdateStats(ListaAuditoria);
                OnPropertyChanged(nameof(Registros));

                // Quick diagnostic: surface metadata of the first loaded row so we can confirm mapping
                try
                {
                    if (ListaAuditoria.Count > 0)
                    {
                        var f = ListaAuditoria[0];
                        var msg = $"DBG: first row -> Rol='{f.Rol}' Sesion='{f.SesionId}' Equipo='{f.Equipo}' Ver='{f.VersionApp}'";
                        ShowSnackbar(msg, 6);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                try { MensajeError = ex.Message; OnPropertyChanged(nameof(MensajeError)); } catch { }
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
                    // If detalles is JSON, prefer showing that JSON but also try to surface metadata
                    if (!string.IsNullOrWhiteSpace(detalles) && detalles.TrimStart().StartsWith("{"))
                    {
                        var node = JsonNode.Parse(detalles) as JsonObject ?? new JsonObject();

                        // If the Detalles JSON already contains metadata keys, prefer those values when the model lacks them
                        try
                        {
                            if (string.IsNullOrWhiteSpace(r.Rol) && node.TryGetPropertyValue("Rol", out var vRol) && vRol is JsonNode jrRol && jrRol.GetValue<string>() is string sRol)
                                r.Rol = sRol;
                        }
                        catch { }
                        try
                        {
                            if (string.IsNullOrWhiteSpace(r.Area) && node.TryGetPropertyValue("Area", out var vArea) && vArea is JsonNode jrArea && jrArea.GetValue<string>() is string sArea)
                                r.Area = sArea;
                        }
                        catch { }
                        try
                        {
                            if (string.IsNullOrWhiteSpace(r.SesionId) && node.TryGetPropertyValue("SesionId", out var vSes) && vSes is JsonNode jrSes && jrSes.GetValue<string>() is string sSes)
                                r.SesionId = sSes;
                        }
                        catch { }
                        try
                        {
                            if (string.IsNullOrWhiteSpace(r.Equipo) && node.TryGetPropertyValue("Equipo", out var vEq) && vEq is JsonNode jrEq && jrEq.GetValue<string>() is string sEq)
                                r.Equipo = sEq;
                        }
                        catch { }
                        try
                        {
                            if (string.IsNullOrWhiteSpace(r.VersionApp) && node.TryGetPropertyValue("VersionApp", out var vVer) && vVer is JsonNode jrVer && jrVer.GetValue<string>() is string sVer)
                                r.VersionApp = sVer;
                        }
                        catch { }

                        // Ensure printed JSON surfaces non-empty metadata from either the JSON or the model.
                        void MergeMetadata(string key, string? modelVal)
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

                                if (!string.IsNullOrWhiteSpace(modelVal))
                                {
                                    // prefer model value when present
                                    node[key] = modelVal;
                                }
                                else if (!has && !string.IsNullOrWhiteSpace(jsonVal))
                                {
                                    node[key] = jsonVal;
                                }
                            }
                            catch { }
                        }

                        MergeMetadata("Rol", r.Rol);
                        MergeMetadata("Area", r.Area);
                        MergeMetadata("SesionId", r.SesionId);
                        MergeMetadata("Equipo", r.Equipo);
                        MergeMetadata("VersionApp", r.VersionApp);

                        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    }
                }
                catch
                {
                    // fall back to plain formatting
                }

                // Non-JSON or parse failed: build a JSON object with Detalles and metadata
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
                    // last resort: plain concatenation
                    var sb = new System.Text.StringBuilder();
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

        public ICommand ActualizarCommand { get; }
        public ICommand AplicarFiltrosCommand { get; }
        public ICommand LimpiarFiltrosCommand { get; }
        public ICommand RotateHmacCommand { get; }
        public ICommand RotateEncCommand { get; }
        public ICommand ExportarCsvCommand { get; }
        public ICommand BackupNowCommand { get; }
        public ICommand ScheduleBackupCommand { get; }
        public ICommand CancelBackupCommand { get; }

        public string BackupTimeText { get; set; } = "02:00";

        public string MensajeError { get; set; } = string.Empty;
        public int Registros => ListaAuditoria.Count;
        public int RegistrosOk { get; set; }
        public int RegistrosError { get; set; }
        public string TiposEnVista { get; set; } = string.Empty;
        public string TextoBusqueda { get; set; } = string.Empty;
        public System.Collections.ObjectModel.ObservableCollection<string> Usuarios { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<string> Modulos { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<string> Acciones { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<string> Resultados { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<string> Tipos { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<string> Roles { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<string> Areas { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<string> Severidades { get; } = new();
        public string? AccionSeleccionada { get; set; }
        public string? ResultadoSeleccionado { get; set; }
        public string? TipoSeleccionado { get; set; }
        public string? RolSeleccionado { get; set; }
        public string? AreaSeleccionada { get; set; }
        public string? SeveridadSeleccionada { get; set; }
        public string? UsuarioSeleccionado { get; set; }
        public string? ModuloSeleccionado { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string FiltroPacienteId { get; set; } = string.Empty;
        public string FiltroCitaId { get; set; } = string.Empty;
        public string FiltroSesionId { get; set; } = string.Empty;
        public bool MostrarSoloOperacionesHerramientas { get; set; }

        public bool IsAdmin
        {
            get
            {
                try
                {
                    var role = ClinicaLongevidadApp.Services.Sesion.RolActual;
                    if (string.IsNullOrWhiteSpace(role)) return false;
                    var r = role.Trim().ToLowerInvariant();
                    return r.Contains("admin") || r.Contains("administr") || r.Contains("administrador");
                }
                catch { return false; }
            }
        }

        private Services.BackupService? _backupService;

        public AuditoriaViewModel(Services.AuditoriaService? auditoriaService)
        {
            _auditoriaService = auditoriaService;
            // Minimal initialization; real implementation populates collections and commands
            Usuarios.Add("-- Todos --");
            Modulos.Add("-- Todos --");
            Acciones.Add("-- Todos --");
            Resultados.Add("-- Todos --");
            Tipos.Add("-- Todos --");
            Roles.Add("-- Todos --");
            Areas.Add("-- Todos --");
            Severidades.Add("-- Todos --");
            // create admin service using connection string saved in application properties if available
            string? conn = null;
            try { conn = Application.Current.Properties["AuditConnectionString"] as string; } catch { }
            if (!string.IsNullOrWhiteSpace(conn))
            {
                _adminService = new AuditAdminService(conn!);
            }

            ActualizarCommand = new RelayCommand(async _ => await EjecutarActualizarAsync());
            AplicarFiltrosCommand = new RelayCommand(async _ => await AplicarFiltrosAsync());
            LimpiarFiltrosCommand = new RelayCommand(async _ => await LimpiarFiltrosAsync());
            RotateHmacCommand = new RelayCommand(async _ => await RotateHmacAsync());
            RotateEncCommand = new RelayCommand(async _ => await RotateEncAsync());
            ExportarCsvCommand = new RelayCommand(async _ => await ExportarCsvAsync());

            BackupNowCommand = new RelayCommand(async _ => await BackupNowAsync());
            ScheduleBackupCommand = new RelayCommand(_ => ScheduleBackup());
            CancelBackupCommand = new RelayCommand(_ => CancelScheduledBackup());

            CopiarSesionIdCommand = new RelayCommand(_ => CopySesionId());
            CopiarKeyVersionsCommand = new RelayCommand(_ => CopyKeyVersions());

            // reflect current session admin status in the view model and update on session changes
            Sesion.SessionChanged += () => OnPropertyChanged(nameof(IsAdmin));
        }

        public void Cleanup()
        {
            // cleanup backup service if scheduled
            try { _backupService?.Dispose(); } catch { }
        }

        private async System.Threading.Tasks.Task BackupNowAsync()
        {
            try
            {
                string? conn = null;
                try { conn = Application.Current.Properties["AuditConnectionString"] as string; } catch { }
                conn ??= Environment.GetEnvironmentVariable("AUDIT_DB") ?? "Data Source=auditoria.db";

                var svc = new Services.BackupService();
                var path = await System.Threading.Tasks.Task.Run(() => svc.CreateBackup(conn));
                MessageBox.Show($"Backup creado: {path}", "Copia de seguridad", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                try { MessageBox.Show("Error creando copia: " + ex.Message, "Copia de seguridad", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }

        private void ScheduleBackup()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(BackupTimeText))
                {
                    MessageBox.Show("Introduce la hora en formato HH:mm", "Programar copia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!TimeSpan.TryParseExact(BackupTimeText, "hh\\:mm", System.Globalization.CultureInfo.InvariantCulture, out var ts))
                {
                    // try general parse
                    if (!TimeSpan.TryParse(BackupTimeText, out ts))
                    {
                        MessageBox.Show("Formato de hora inválido. Usa HH:mm.", "Programar copia", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                string? conn = null;
                try { conn = Application.Current.Properties["AuditConnectionString"] as string; } catch { }
                conn ??= Environment.GetEnvironmentVariable("AUDIT_DB") ?? "Data Source=auditoria.db";

                _backupService ??= new Services.BackupService();
                var ok = _backupService.ScheduleDailyBackup(ts, conn);
                if (ok) MessageBox.Show($"Backup programado a las {ts:hh\\:mm}", "Programar copia", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                try { MessageBox.Show("Error programando copia: " + ex.Message, "Programar copia", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }

        private void CancelScheduledBackup()
        {
            try
            {
                _backupService?.CancelScheduledBackup();
                try { MessageBox.Show("Copia programada cancelada.", "Cancelar copia", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
            }
            catch (Exception ex)
            {
                try { MessageBox.Show("Error cancelando copia: " + ex.Message, "Cancelar copia", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }

        private static Services.IKeyRotationProvider? CreateRotationProvider()
        {
            // prefer Azure if configured, otherwise local
            try
            {
                var kv = Environment.GetEnvironmentVariable("KEYVAULT_URI");
                if (!string.IsNullOrWhiteSpace(kv))
                {
                    try { return new Services.AzureKeyRotationProvider(); } catch { }
                }
            }
            catch { }

            try { return new Services.LocalKeyRotationProvider(); } catch { return null; }
        }

        private static byte[] GenerateRandomKey(int bytes)
        {
            var key = new byte[bytes];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(key);
            return key;
        }

        private static DateTime ParseFecha(string? fh)
        {
            if (string.IsNullOrWhiteSpace(fh))
                return DateTime.MinValue;

            // Try ISO 8601 round-trip first (with kind preserved)
            if (DateTime.TryParseExact(fh, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                if (dt.Kind == DateTimeKind.Utc) return dt.ToLocalTime();
                return dt;
            }

            // Try parsing as DateTimeOffset to safely handle timezone offsets
            if (DateTimeOffset.TryParse(fh, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            {
                return dto.ToLocalTime().DateTime;
            }

            // Fallback to invariant parse with AssumeLocal/AssumeUniversal heuristics
            if (DateTime.TryParse(fh, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
            {
                return dt.ToLocalTime();
            }

            // Last resort: plain TryParse using current culture
            if (DateTime.TryParse(fh, out dt)) return dt;

            return DateTime.MinValue;
        }

        private void UpdateStats(IEnumerable<Models.AuditoriaModel> rows)
        {
            try
            {
                var list = rows ?? Enumerable.Empty<Models.AuditoriaModel>();
                RegistrosOk = list.Count(r => string.Equals((r.Resultado ?? string.Empty).Trim(), "OK", StringComparison.OrdinalIgnoreCase));
                RegistrosError = list.Count(r => !string.IsNullOrWhiteSpace(r.Resultado) && !string.Equals(r.Resultado.Trim(), "OK", StringComparison.OrdinalIgnoreCase));
                var tipos = list.Select(r => (r.Tipo ?? string.Empty).Trim())
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();
                TiposEnVista = tipos.Count == 0 ? string.Empty : string.Join(", ", tipos);

                OnPropertyChanged(nameof(RegistrosOk));
                OnPropertyChanged(nameof(RegistrosError));
                OnPropertyChanged(nameof(TiposEnVista));
            }
            catch { }
        }

        private async System.Threading.Tasks.Task RotateHmacAsync()
        {
            try
            {
                var provider = CreateRotationProvider();
                if (provider is null)
                {
                    MessageBox.Show("No key rotation provider available.", "Rotar HMAC", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var key = GenerateRandomKey(32);
                await System.Threading.Tasks.Task.Run(() => provider.PersistHmacKey(key));
                MessageBox.Show("HMAC key rotated (persisted).", "Rotar HMAC", MessageBoxButton.OK, MessageBoxImage.Information);

                // After rotation, try to run VerifyIntegrity if audit DB connection is available
                try
                {
                    string? conn = null;
                    try { conn = Application.Current.Properties["AuditConnectionString"] as string; } catch { }
                    conn ??= Environment.GetEnvironmentVariable("AUDIT_DB") ?? "Data Source=auditoria.db";

                    // Run verify in background to avoid UI freeze
                    var svc = new Services.AuditoriaService(conn);
                    var errors = await System.Threading.Tasks.Task.Run(() => svc.VerifyIntegrity());
                    if (errors is null || errors.Count == 0)
                    {
                        MessageBox.Show("VerifyIntegrity: OK (no errors)", "Rotar HMAC - Verificar", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        var preview = string.Join("\n", errors.Take(5));
                        MessageBox.Show($"VerifyIntegrity returned {errors.Count} error(s). First:\n{preview}", "Rotar HMAC - Verificar", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    try { MessageBox.Show("VerifyIntegrity failed: " + ex.Message, "Rotar HMAC - Verificar", MessageBoxButton.OK, MessageBoxImage.Warning); } catch { }
                }
            }
            catch (Exception ex)
            {
                try { MessageBox.Show("Failed to rotate HMAC: " + ex.Message, "Rotar HMAC", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }

        private async System.Threading.Tasks.Task RotateEncAsync()
        {
            try
            {
                var provider = CreateRotationProvider();
                if (provider is null)
                {
                    MessageBox.Show("No key rotation provider available.", "Rotar ENC", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var key = GenerateRandomKey(32);
                await System.Threading.Tasks.Task.Run(() => provider.PersistEncryptionKey(key));
                MessageBox.Show("Encryption key rotated (persisted).", "Rotar ENC", MessageBoxButton.OK, MessageBoxImage.Information);

                // After rotation, try to run VerifyIntegrity if audit DB connection is available
                try
                {
                    string? conn = null;
                    try { conn = Application.Current.Properties["AuditConnectionString"] as string; } catch { }
                    conn ??= Environment.GetEnvironmentVariable("AUDIT_DB") ?? "Data Source=auditoria.db";

                    var svc = new Services.AuditoriaService(conn);
                    var errors = await System.Threading.Tasks.Task.Run(() => svc.VerifyIntegrity());
                    if (errors is null || errors.Count == 0)
                    {
                        MessageBox.Show("VerifyIntegrity: OK (no errors)", "Rotar ENC - Verificar", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        var preview = string.Join("\n", errors.Take(5));
                        MessageBox.Show($"VerifyIntegrity returned {errors.Count} error(s). First:\n{preview}", "Rotar ENC - Verificar", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    try { MessageBox.Show("VerifyIntegrity failed: " + ex.Message, "Rotar ENC - Verificar", MessageBoxButton.OK, MessageBoxImage.Warning); } catch { }
                }
            }
            catch (Exception ex)
            {
                try { MessageBox.Show("Failed to rotate encryption key: " + ex.Message, "Rotar ENC", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }

        private async System.Threading.Tasks.Task AplicarFiltrosAsync()
        {
            try
            {
                // Fetch a superset and then apply client-side filters
                List<Models.AuditoriaModel> rows = null;
                if (_auditoriaService is not null)
                {
                    // Request full dataset for filtering (pass 0 => no LIMIT)
                    rows = await System.Threading.Tasks.Task.Run(() => _auditoriaService.GetRecentAudits(0));
                }
                else if (_adminService is not null)
                {
                    var temp = await System.Threading.Tasks.Task.Run(() => _adminService.GetRecentAudits(0));
                    rows = temp.Select(r => new Models.AuditoriaModel
                    {
                        Id = r.Id,
                        FechaHora = ParseFecha(r.FechaHora),
                        UsuarioAdmin = r.UsuarioAdmin,
                        Accion = r.Accion,
                        Modulo = r.Modulo,
                        UsuarioAfectado = r.UsuarioAfectado,
                        Resultado = r.Resultado,
                        Detalles = string.Empty
                    }).ToList();
                }

                if (rows is null)
                    return;

                // apply filters
                var query = rows.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(TextoBusqueda))
                {
                    var t = TextoBusqueda.Trim();
                    query = query.Where(r =>
                        (r.UsuarioAdmin ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (r.Accion ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (r.Modulo ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (r.Resultado ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (r.UsuarioAfectado ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (r.Detalles ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase)
                    );
                }

                if (!string.IsNullOrWhiteSpace(UsuarioSeleccionado) && UsuarioSeleccionado != "-- Todos --")
                {
                    query = query.Where(r => string.Equals(r.UsuarioAdmin?.Trim(), UsuarioSeleccionado.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(ModuloSeleccionado) && ModuloSeleccionado != "-- Todos --")
                {
                    query = query.Where(r => string.Equals(r.Modulo?.Trim(), ModuloSeleccionado.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                if (MostrarSoloOperacionesHerramientas)
                {
                    query = query.Where(r =>
                        (!string.IsNullOrWhiteSpace(r.Modulo) && r.Modulo.IndexOf("RotateKeys", StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!string.IsNullOrWhiteSpace(r.Tipo) && r.Tipo.IndexOf("Operación", StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!string.IsNullOrWhiteSpace(r.Accion) && (
                            r.Accion.IndexOf("export", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            r.Accion.IndexOf("restore", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            r.Accion.IndexOf("replay", StringComparison.OrdinalIgnoreCase) >= 0))
                    );
                }

                // Detalles-based filters
                if (!string.IsNullOrWhiteSpace(FiltroPacienteId))
                {
                    query = query.Where(r => ClinicaLongevidadApp.Helpers.AuditoriaDetallesHelper.CoincideCampo(r.Detalles, "PacienteId", FiltroPacienteId));
                }

                if (!string.IsNullOrWhiteSpace(FiltroCitaId))
                {
                    query = query.Where(r => ClinicaLongevidadApp.Helpers.AuditoriaDetallesHelper.CoincideCampo(r.Detalles, "CitaId", FiltroCitaId));
                }

                if (!string.IsNullOrWhiteSpace(FiltroSesionId))
                {
                    query = query.Where(r => ClinicaLongevidadApp.Helpers.AuditoriaDetallesHelper.CoincideCampo(r.Detalles, "SesionId", FiltroSesionId));
                }

                // Fecha filters
                if (FechaDesde.HasValue)
                {
                    query = query.Where(r => r.FechaHora >= FechaDesde.Value.Date);
                }
                if (FechaHasta.HasValue)
                {
                    query = query.Where(r => r.FechaHora <= FechaHasta.Value.Date.AddDays(1).AddTicks(-1));
                }

                // Update UI collection
                ListaAuditoria.Clear();
                foreach (var m in query)
                {
                    ListaAuditoria.Add(m);
                }

                // Update computed counters and visible types
                UpdateStats(ListaAuditoria);
                OnPropertyChanged(nameof(Registros));
            }
            catch (Exception ex)
            {
                try { MensajeError = ex.Message; OnPropertyChanged(nameof(MensajeError)); } catch { }
            }
        }

        private async System.Threading.Tasks.Task LimpiarFiltrosAsync()
        {
            try
            {
                TextoBusqueda = string.Empty;
                UsuarioSeleccionado = null;
                ModuloSeleccionado = null;
                AccionSeleccionada = null;
                ResultadoSeleccionado = null;
                TipoSeleccionado = null;
                RolSeleccionado = null;
                AreaSeleccionada = null;
                MostrarSoloOperacionesHerramientas = false;
                FiltroPacienteId = string.Empty;
                FiltroCitaId = string.Empty;
                FiltroSesionId = string.Empty;
                FechaDesde = null;
                FechaHasta = null;

                // Refresh full dataset
                await EjecutarActualizarAsync();
                OnPropertyChanged(string.Empty);
            }
            catch (Exception ex)
            {
                try { MensajeError = ex.Message; OnPropertyChanged(nameof(MensajeError)); } catch { }
            }
        }

        private async System.Threading.Tasks.Task EjecutarActualizarAsync()
        {
            try
            {
                ListaAuditoria.Clear();

                List<Models.AuditoriaModel> rows = null;
                if (_auditoriaService is not null)
                {
                    // Mostrar todos (límite mayor) al pulsar Actualizar en el panel principal
                    rows = await System.Threading.Tasks.Task.Run(() => _auditoriaService.GetRecentAudits(0));
                }
                else if (_adminService is not null)
                {
                    var temp = await System.Threading.Tasks.Task.Run(() => _adminService.GetRecentAudits(0));
                    rows = temp.Select(r => new Models.AuditoriaModel
                    {
                        Id = r.Id,
                        FechaHora = ParseFecha(r.FechaHora),
                        UsuarioAdmin = r.UsuarioAdmin,
                        Accion = r.Accion,
                        Modulo = r.Modulo,
                        UsuarioAfectado = r.UsuarioAfectado,
                        Resultado = r.Resultado,
                        Detalles = string.Empty
                    }).ToList();
                }

                if (rows != null)
                {
                    foreach (var model in rows)
                    {
                        ListaAuditoria.Add(model);
                    }
                }

                // Update computed counters and visible types
                UpdateStats(ListaAuditoria);
                OnPropertyChanged(nameof(Registros));
            }
            catch (Exception ex)
            {
                try { MensajeError = ex.Message; OnPropertyChanged(nameof(MensajeError)); } catch { }
            }
        }
    }
}
