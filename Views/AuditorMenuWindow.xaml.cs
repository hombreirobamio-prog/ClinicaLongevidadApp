using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using Microsoft.Win32;

#pragma warning disable CS8600

namespace ClinicaLongevidadApp.Views
{
    public partial class AuditorMenuWindow : Window
    {
        private string? _selectedZipPath;
        private string? _lastManifestPath;
        private string _lastHash = string.Empty;
        // Indicates the HMAC key was auto-loaded from secure storage during window load
#pragma warning disable CS0414
        private bool _hmacKeyAutoLoaded = false;
#pragma warning restore CS0414

        public AuditorMenuWindow()
        {
            InitializeComponent();
            Loaded += AuditorMenuWindow_Loaded;
        }

        // Find DataGrid(s) that look like the audit grid and ensure they are always sorted by the
        // "Fecha"/"Fecha / Hora" column in descending order. This attaches a LayoutUpdated handler
        // that reapplies the sort so the display stays continuous.
        private void RegisterAuditGridSorter()
        {
            try
            {
                foreach (var dg in FindVisualChildren<DataGrid>(this))
                {
                    // Look for a column header that contains Fecha or Fecha / Hora
                    var hasFecha = dg.Columns.Any(c => (c.Header?.ToString() ?? string.Empty).IndexOf("Fecha", StringComparison.OrdinalIgnoreCase) >= 0
                                                      || (c.Header?.ToString() ?? string.Empty).IndexOf("Hora", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!hasFecha) continue;

                    // Attach handler that reapplies sorting
                    dg.LayoutUpdated -= AuditGrid_LayoutUpdated;
                    dg.LayoutUpdated += AuditGrid_LayoutUpdated;
                }

            }
            catch { }
        }

        private static IEnumerable<string> FindAuditZips()
        {
            var folders = new[]
            {
                ClinicaLongevidadApp.Services.AppPaths.AuditArtifactsDir,
                ClinicaLongevidadApp.Services.AppPaths.BackupsDir
            };

            return folders
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.GetFiles(folder, "*.zip"))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private void AuditGrid_LayoutUpdated(object? sender, EventArgs e)
        {
            if (sender is not DataGrid dg) return;

            try
            {
                // find the column that looks like the timestamp column
                var col = dg.Columns.FirstOrDefault(c => (c.Header?.ToString() ?? string.Empty).IndexOf("Fecha", StringComparison.OrdinalIgnoreCase) >= 0
                                                        || (c.Header?.ToString() ?? string.Empty).IndexOf("Hora", StringComparison.OrdinalIgnoreCase) >= 0);
                if (col == null) return;

                // if there is a SortMemberPath, use it; otherwise try binding path for bound columns
                var sortPath = col.SortMemberPath;
                if (string.IsNullOrWhiteSpace(sortPath) && col is DataGridBoundColumn bound)
                {
                    // try to extract the path from the binding
                    var binding = bound.Binding as System.Windows.Data.Binding;
                    sortPath = binding?.Path?.Path ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(sortPath))
                {
                    // fallback: sort by the displayed string in the column by using the column index
                    // clear existing sorts and set column header direction for visual feedback
                    dg.Items.SortDescriptions.Clear();
                    foreach (var c in dg.Columns) c.SortDirection = null;
                    col.SortDirection = ListSortDirection.Descending;
                    dg.Items.Refresh();
                    return;
                }

                // Apply sort
                var existing = dg.Items.SortDescriptions.FirstOrDefault();
                if (existing.PropertyName == sortPath && existing.Direction == ListSortDirection.Descending) return;

                dg.Items.SortDescriptions.Clear();
                dg.Items.SortDescriptions.Add(new SortDescription(sortPath, ListSortDirection.Descending));
                foreach (var c in dg.Columns) c.SortDirection = null;
                col.SortDirection = ListSortDirection.Descending;
                dg.Items.Refresh();
            }
            catch { }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) yield return t;

                foreach (var childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

#pragma warning restore CS8600

        private void BtnSaveHmacKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pb = this.FindName("PwdHmacKey") as System.Windows.Controls.PasswordBox;
                var keyText = pb?.Password ?? string.Empty;
                if (string.IsNullOrWhiteSpace(keyText))
                {
                    TxtLogAppend("No hay clave HMAC en el campo para guardar.");
                    return;
                }

                if (!TryDecodeKey(keyText, out var keyBytes))
                {
                    TxtLogAppend("No se pudo decodificar la clave HMAC (esperado Base64 o hex). No guardada.");
                    return;
                }

                // Save encrypted key on Windows using DPAPI; on other platforms fall back to plaintext .txt
                if (OperatingSystem.IsWindows())
                {
                    ClinicaLongevidadApp.Services.HmacKeyStore.SaveEncryptedKey(keyBytes);
                    TxtLogAppend("Clave HMAC guardada de forma segura en LocalAppData.");
                }
                else
                {
                    try
                    {
                        var path = ClinicaLongevidadApp.Services.HmacKeyStore.GetKeyFilePath();
                        var txtPath = System.IO.Path.ChangeExtension(path, ".txt");
                        System.IO.File.WriteAllText(txtPath, Convert.ToBase64String(keyBytes));
                        TxtLogAppend("Clave HMAC guardada en texto plano en LocalAppData (.txt) — sólo para entornos no-Windows.");
                    }
                    catch (Exception ex)
                    {
                        TxtLogAppend("Error guardando clave HMAC en texto plano: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error guardando clave HMAC: " + ex.Message);
            }
        }

        private void BtnDeleteHmacKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var keyPath = ClinicaLongevidadApp.Services.HmacKeyStore.GetKeyFilePath();
                var txtPath = System.IO.Path.ChangeExtension(keyPath, ".txt");
                bool deletedAny = false;

                if (System.IO.File.Exists(keyPath))
                {
                    System.IO.File.Delete(keyPath);
                    deletedAny = true;
                }

                if (System.IO.File.Exists(txtPath))
                {
                    System.IO.File.Delete(txtPath);
                    deletedAny = true;
                }

                // Clear UI password and auto-loaded flag
                var pb = this.FindName("PwdHmacKey") as System.Windows.Controls.PasswordBox;
                if (pb != null) pb.Password = string.Empty;
                _hmacKeyAutoLoaded = false;

                if (deletedAny)
                {
                    TxtLogAppend("Clave HMAC eliminada de LocalAppData (archivos .key/.txt si existían).");
                }
                else
                {
                    TxtLogAppend("No existe clave HMAC guardada.");
                }
                try
                {
                    var ev = new ClinicaLongevidadApp.Models.AuditoriaEvento
                    {
                        Accion = "Eliminar clave HMAC",
                        Modulo = "AuditorMenu",
                        UsuarioAdmin = Environment.UserName ?? string.Empty,
                        Resultado = deletedAny,
                        FechaHora = DateTime.UtcNow,
                        Detalles = deletedAny ? "Deleted key files" : "No key present"
                    };
                    App.AuditoriaService?.RegistrarEvento(ev);
                }
                catch { }
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error eliminando clave HMAC: " + ex.Message);
            }
        }

        private async void BtnValidateHmac_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pwdBox = this.FindName("PwdHmacKey") as System.Windows.Controls.PasswordBox;
                var keyText = pwdBox?.Password ?? string.Empty;
                if (string.IsNullOrWhiteSpace(keyText))
                {
                    TxtLogAppend("Clave HMAC no indicada.");
                    return;
                }

                byte[] keyBytes;
                // Try Base64 first, fall back to hex
                if (!TryDecodeKey(keyText, out keyBytes))
                {
                    TxtLogAppend("No se pudo decodificar la clave HMAC (esperado Base64 o hex).");
                    return;
                }

                var dg = this.FindName("DgManifest") as System.Windows.Controls.DataGrid;
                if (dg == null || dg.ItemsSource is not System.Collections.IEnumerable items)
                {
                    TxtLogAppend("No hay manifiesto cargado para validar HMAC.");
                    return;
                }

                var list = new System.Collections.Generic.List<ManifestEntry>();
                foreach (var o in items)
                {
                    if (o is ManifestEntry me) list.Add(me);
                }

                if (list.Count == 0)
                {
                    TxtLogAppend("Manifiesto sin entradas para validar HMAC.");
                    return;
                }

                TxtLogAppend("Validando HMAC en las entradas del manifiesto...");

                await Task.Run(() => ValidateHmacEntries(keyBytes, list));

                TxtLogAppend("Validación HMAC completada.");

                // Register audit event
                try
                {
                    var ev = new ClinicaLongevidadApp.Models.AuditoriaEvento
                    {
                        Accion = "Validar HMAC",
                        Modulo = "AuditorMenu",
                        UsuarioAdmin = Environment.UserName ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.UtcNow,
                        Detalles = $"Manifest={_lastManifestPath ?? "<none>"}, Entries={list.Count}"
                    };
                    App.AuditoriaService?.RegistrarEvento(ev);
                }
                catch { }
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error validando HMAC: " + ex.Message);
            }
        }

        private void ValidateHmacEntries(byte[] keyBytes, List<ManifestEntry> list)
        {
            foreach (var entry in list)
            {
                try
                {
                        if (string.IsNullOrWhiteSpace(entry.Hmac))
                        {
                            entry.HmacResult = "NO_HMAC";
                        }
                        else if (!File.Exists(entry.FilePath))
                        {
                            entry.HmacResult = "MISSING";
                        }
                        else
                        {
                            using var fs = File.OpenRead(entry.FilePath);
                            using var h = new HMACSHA256(keyBytes);
                            var computed = h.ComputeHash(fs);
                            var hex = Convert.ToHexString(computed);
                            var b64 = Convert.ToBase64String(computed);

                            var received = entry.Hmac.Trim();
                            bool ok = string.Equals(received, hex, StringComparison.OrdinalIgnoreCase) || string.Equals(received, b64, StringComparison.Ordinal);
                            // Use a human-friendly label so it shows clearly in the 'Estado' column when combined
                            entry.HmacResult = ok ? "OK HMAC" : "HMAC_MISMATCH";
                        }
                }
                catch (Exception ex)
                {
                    entry.HmacResult = "ERR_HMAC: " + ex.Message;
                }

                // Append HMAC result into the Status column so auditors see a combined status (e.g. "OK, OK HMAC")
                entry.Status = CombineStatus(entry.Status, entry.HmacResult);

                Application.Current?.Dispatcher?.Invoke(() => { var d = this.FindName("DgManifest") as System.Windows.Controls.DataGrid; d?.Items.Refresh(); });
            }
        }

        private static string CombineStatus(string existing, string add)
        {
            if (string.IsNullOrWhiteSpace(existing) || existing == "PENDING") return add;
            return existing + ", " + add;
        }

        private void OpenFileInExplorer(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("explorer", "/select,\"" + path + "\"") { UseShellExecute = true };
                    System.Diagnostics.Process.Start(psi);
                }
                else if (OperatingSystem.IsLinux())
                {
                    var dir = System.IO.Path.GetDirectoryName(path) ?? path;
                    var psi = new System.Diagnostics.ProcessStartInfo("xdg-open", dir) { UseShellExecute = true };
                    System.Diagnostics.Process.Start(psi);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    var dir = System.IO.Path.GetDirectoryName(path) ?? path;
                    var psi = new System.Diagnostics.ProcessStartInfo("open", dir) { UseShellExecute = true };
                    System.Diagnostics.Process.Start(psi);
                }
            }
            catch { }
        }

        private static bool TryDecodeKey(string text, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Try Base64
            try
            {
                bytes = Convert.FromBase64String(text.Trim());
                if (bytes.Length > 0) return true;
            }
            catch { }

            // Try hex (tolerant: accepts 0x, spaces, dashes and colons)
            try
            {
                var s = text.Trim();
                s = s.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
                         .Replace(" ", string.Empty)
                         .Replace("-", string.Empty)
                         .Replace(":", string.Empty);
                if (s.Length % 2 != 0) return false;
                var outb = new byte[s.Length / 2];
                for (int i = 0; i < outb.Length; i++)
                {
                    outb[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
                }
                bytes = outb;
                return true;
            }
            catch { }

            return false;
        }

        // ===== Manifest parsing / verification (MVP, in-window) =====
        private class ManifestEntry
        {
            public string FilePath { get; set; } = string.Empty;
            public string Sha256 { get; set; } = string.Empty;
            public string Hmac { get; set; } = string.Empty;
            // Result of HMAC validation (HMAC_OK / HMAC_MISMATCH / NO_HMAC / ERR...)
            public string HmacResult { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }

        private System.Collections.Generic.List<ManifestEntry> ParseManifestFile(string path)
        {
            var entries = new System.Collections.Generic.List<ManifestEntry>();
            try
            {
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                ManifestEntry? current = null;
                foreach (var raw in lines)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    var line = raw.Trim();
                    if (line.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (current != null) entries.Add(current);
                        var fp = line.Substring(5).Trim();
                        current = new ManifestEntry { FilePath = fp, Status = "PENDING" };
                    }
                    else if (current != null)
                    {
                        if (line.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
                        {
                            current.Sha256 = line.Substring(7).Trim();
                        }
                        else if (line.StartsWith("HMAC:", StringComparison.OrdinalIgnoreCase))
                        {
                            current.Hmac = line.Substring(5).Trim();
                        }
                    }
                }

                if (current != null) entries.Add(current);
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error parseando manifiesto: " + ex.Message);
            }

            return entries;
        }

        private async void BtnLoadManifest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "Manifest text|*.txt",
                    Title = "Seleccionar manifiesto de auditoría",
                    InitialDirectory = ClinicaLongevidadApp.Services.AppPaths.AuditManifestsDir
                };
                if (dlg.ShowDialog(this) != true) return;

                _lastManifestPath = dlg.FileName;
                var entries = ParseManifestFile(_lastManifestPath);
                var dg = this.FindName("DgManifest") as System.Windows.Controls.DataGrid;
                if (dg != null) dg.ItemsSource = entries;
                TxtLogAppend($"Manifiesto cargado: {_lastManifestPath} ({entries.Count} entradas)");

                try
                {
                    var ev = new ClinicaLongevidadApp.Models.AuditoriaEvento
                    {
                        Accion = "Cargar manifiesto",
                        Modulo = "AuditorMenu",
                        UsuarioAdmin = Environment.UserName ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.UtcNow,
                        Detalles = _lastManifestPath ?? string.Empty
                    };
                    App.AuditoriaService?.RegistrarEvento(ev);
                }
                catch { }

                // If an HMAC key is already loaded in the PasswordBox, validate automatically
                try
                {
                    var pb = this.FindName("PwdHmacKey") as System.Windows.Controls.PasswordBox;
                    if (pb != null && !string.IsNullOrWhiteSpace(pb.Password) && entries.Count > 0)
                    {
                        if (TryDecodeKey(pb.Password, out var keyBytes))
                        {
                            TxtLogAppend("Clave HMAC detectada en sesión, validando automáticamente...");
                            await Task.Run(() => ValidateHmacEntries(keyBytes, entries));
                            TxtLogAppend("Validación HMAC automática completada.");
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error cargando manifiesto: " + ex.Message);
            }
        }

        private async void BtnVerifyLoadedManifest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dg = this.FindName("DgManifest") as System.Windows.Controls.DataGrid;
                if (dg == null || dg.ItemsSource is not System.Collections.IEnumerable items)
                {
                    TxtLogAppend("No hay manifiesto cargado.");
                    return;
                }

                var list = new System.Collections.Generic.List<ManifestEntry>();
                foreach (var o in items)
                {
                    if (o is ManifestEntry me) list.Add(me);
                }

                if (list.Count == 0)
                {
                    TxtLogAppend("Manifiesto sin entradas.");
                    return;
                }

                TxtLogAppend("Verificando manifiesto (SHA256)...");

                await Task.Run(() =>
                {
                    foreach (var entry in list)
                    {
                        try
                        {
                            if (File.Exists(entry.FilePath))
                            {
                                var calc = ComputeSha256(entry.FilePath);
                                if (string.Equals(calc, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                                {
                                    entry.Status = "OK";
                                }
                                else
                                {
                                    entry.Status = "MISMATCH";
                                }
                            }
                            else
                            {
                                entry.Status = "MISSING";
                            }
                        }
                        catch (Exception ex)
                        {
                            entry.Status = "ERR: " + ex.Message;
                        }

                        Application.Current?.Dispatcher?.Invoke(() => { var d = this.FindName("DgManifest") as System.Windows.Controls.DataGrid; d?.Items.Refresh(); });
                    }
                });

                TxtLogAppend("Verificación completada.");

                try
                {
                    var ev = new ClinicaLongevidadApp.Models.AuditoriaEvento
                    {
                        Accion = "Verificar manifiesto (local)",
                        Modulo = "AuditorMenu",
                        UsuarioAdmin = Environment.UserName ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.UtcNow,
                        Detalles = _lastManifestPath ?? string.Empty
                    };
                    App.AuditoriaService?.RegistrarEvento(ev);
                }
                catch { }
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error verificando manifiesto: " + ex.Message);
            }
        }

        private async void AuditorMenuWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Auto-detect and preselect the most recent ZIP if available
                var found = FindAuditZips().ToList();

                if (found.Count > 0)
                {
                    var latest = System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.OrderByDescending(found, f => System.IO.File.GetLastWriteTimeUtc(f)));
                    if (!string.IsNullOrWhiteSpace(latest))
                    {
                        _selectedZipPath = latest;
                        // fill listbox for visibility
                        LstDetectedZips.Items.Clear();
                        LstDetectedZips.Items.Add(new { Name = System.IO.Path.GetFileName(latest), FullPath = latest });
                        LstDetectedZips.SelectedIndex = 0;
                        TxtLogAppend("Preseleccionado ZIP más reciente: " + latest);
                        TxtLogAppend("Calculando SHA256 ...");
                        var hash = await Task.Run(() => ComputeSha256(_selectedZipPath));
                        _lastHash = hash;
                        TxtLogAppend($"SHA256: {hash}");
                        // NOTE: do not automatically open Explorer on window load - the auditor can open the containing folder
                        // using the "Abrir carpeta contenedora" button or by double-clicking the ZIP in the list.
                    }
                }

                // Try to auto-load an encrypted HMAC key from LocalAppData (DPAPI-protected) or a plaintext fallback
                try
                {
                    // Use platform guard for DPAPI operations (Windows-only)
                    if (OperatingSystem.IsWindows() && ClinicaLongevidadApp.Services.HmacKeyStore.TryLoadDecryptedKey(out var keyBytes))
                    {
                        var pb = this.FindName("PwdHmacKey") as System.Windows.Controls.PasswordBox;
                        if (pb != null)
                        {
                            pb.Password = Convert.ToBase64String(keyBytes);
                            TxtLogAppend("Clave HMAC cargada desde LocalAppData.");
                            _hmacKeyAutoLoaded = true;
                        }
                    }
                    else if (ClinicaLongevidadApp.Services.HmacKeyStore.TryLoadPlaintextKey(out var plain))
                    {
                        var pb = this.FindName("PwdHmacKey") as System.Windows.Controls.PasswordBox;
                        if (pb != null)
                        {
                            pb.Password = Convert.ToBase64String(plain);
                            TxtLogAppend("Clave HMAC (texto) cargada desde LocalAppData (se recomienda cifrarla).");
                        }
                    }
                }
                catch (Exception ex)
                {
                    TxtLogAppend("No se pudo cargar clave HMAC: " + ex.Message);
                }
            }
            catch { }
        }

        private void BtnDetectZips_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LstDetectedZips.Items.Clear();
                var found = FindAuditZips();

                // Remove duplicates and add to listbox
                foreach (var f in System.Linq.Enumerable.Distinct(found))
                {
                    LstDetectedZips.Items.Add(new { Name = System.IO.Path.GetFileName(f), FullPath = f });
                }

                TxtLogAppend($"Detectados {LstDetectedZips.Items.Count} ZIP(s).");
                try
                {
                    var ev = new ClinicaLongevidadApp.Models.AuditoriaEvento
                    {
                        Accion = "Detectar ZIPs",
                        Modulo = "AuditorMenu",
                        UsuarioAdmin = Environment.UserName ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.UtcNow,
                        Detalles = "Count=" + LstDetectedZips.Items.Count
                    };
                    App.AuditoriaService?.RegistrarEvento(ev);
                }
                catch { }
            }
            catch (System.Exception ex)
            {
                TxtLogAppend("Error detectando ZIPs: " + ex.Message);
            }
        }

        private async void LstDetectedZips_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var sel = LstDetectedZips.SelectedItem;
                string? path = null;
                if (sel is System.Windows.Controls.ListBoxItem lbi && lbi.Tag is string p1)
                {
                    path = p1;
                }
                else if (sel is not null)
                {
                    var prop = sel.GetType().GetProperty("FullPath");
                    if (prop != null) path = prop.GetValue(sel) as string;
                    else
                    {
                        // Fallback: if item is string
                        path = sel as string;
                    }
                }

                if (!string.IsNullOrWhiteSpace(path))
                {
                    _selectedZipPath = path;
                    TxtLogAppend("ZIP seleccionado: " + _selectedZipPath);
                    TxtLogAppend("Calculando SHA256 ...");
                    var hash = await Task.Run(() => ComputeSha256(_selectedZipPath));
                    _lastHash = hash;
                    TxtLogAppend($"SHA256: {hash}");
                        try
                        {
                            OpenFileInExplorer(_selectedZipPath);
                        }
                        catch { }
                }
            }
            catch (System.Exception ex)
            {
                TxtLogAppend("Error seleccionando ZIP: " + ex.Message);
            }
        }

        private void BtnOpenContainingFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? path = _selectedZipPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    var sel = LstDetectedZips.SelectedItem;
                    if (sel is System.Windows.Controls.ListBoxItem lbi && lbi.Tag is string p1) path = p1;
                    else if (sel is not null)
                    {
                        var prop = sel.GetType().GetProperty("FullPath");
                        if (prop != null) path = prop.GetValue(sel) as string;
                        else path = sel as string;
                    }
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    TxtLogAppend("No hay ZIP seleccionado para abrir su carpeta.");
                    return;
                }

                try
                {
                    OpenFileInExplorer(path);
                }
                catch { }
                try
                {
                    var ev = new ClinicaLongevidadApp.Models.AuditoriaEvento
                    {
                        Accion = "Abrir carpeta contenedora",
                        Modulo = "AuditorMenu",
                        UsuarioAdmin = Environment.UserName ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.UtcNow,
                        Detalles = path ?? string.Empty
                    };
                    App.AuditoriaService?.RegistrarEvento(ev);
                }
                catch { }
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error abriendo carpeta contenedora: " + ex.Message);
            }
        }

        private async void BtnSelectZip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*",
                    Title = "Seleccionar ZIP de auditoría",
                    RestoreDirectory = true
                };

                // Prefer the active artifact folders used by the application.
                try
                {
                    var preferLocal = ClinicaLongevidadApp.Services.AppPaths.AuditArtifactsDir;
                    if (System.IO.Directory.Exists(preferLocal))
                    {
                        dlg.InitialDirectory = preferLocal;
                    }
                    else
                    {
                        var prefer = ClinicaLongevidadApp.Services.AppPaths.BackupsDir;
                        if (System.IO.Directory.Exists(prefer)) dlg.InitialDirectory = prefer;
                        else dlg.InitialDirectory = ClinicaLongevidadApp.Services.AppPaths.BaseDir;
                    }
                }
                catch
                {
                    // ignore and let dialog pick default
                }

                if (dlg.ShowDialog(this) != true) return;

                _selectedZipPath = dlg.FileName;
                TxtLogAppend($"ZIP seleccionado: {_selectedZipPath}");
                TxtLogAppend("Calculando SHA256 ...");

                var hash = await Task.Run(() => ComputeSha256(_selectedZipPath));
                _lastHash = hash;
                TxtLogAppend($"SHA256: {hash}");

                try
                {
                    var ev = new ClinicaLongevidadApp.Models.AuditoriaEvento
                    {
                        Accion = "Seleccionar ZIP",
                        Modulo = "AuditorMenu",
                        UsuarioAdmin = Environment.UserName ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.UtcNow,
                        Detalles = _selectedZipPath ?? string.Empty
                    };
                    App.AuditoriaService?.RegistrarEvento(ev);
                }
                catch { }
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error calculando hash: " + ex.Message);
            }
        }

        private static string ComputeSha256(string path)
        {
            using var fs = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(fs);
            return Convert.ToHexString(hash);
        }

        private void BtnVerifyManifest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog { Filter = "Manifest text|*.txt", Title = "Seleccionar manifiesto de auditoría" };
                if (dlg.ShowDialog(this) != true) return;

                _lastManifestPath = dlg.FileName;
                TxtLogAppend($"Manifiesto: {_lastManifestPath}");

                // Ejecuta el script de verificación existente (scripts\verify_audit_manifest.ps1)
                var scriptName = "verify_audit_manifest.ps1";
                string? scriptPath = null;
                var tried = new List<string>();

                // Candidate: app base + scripts
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    tried.Add(System.IO.Path.Combine(baseDir, "scripts", scriptName));

                    // walk up a few parents to find a repository-level 'scripts' folder (when running from bin) 
                    var di = new DirectoryInfo(baseDir);
                    for (int i = 0; i < 6 && di != null; i++)
                    {
                        var cand = System.IO.Path.Combine(di.FullName, "scripts", scriptName);
                        tried.Add(cand);
                        if (File.Exists(cand)) { scriptPath = cand; break; }
                        di = di.Parent;
                    }

                    // fallback: baseDir\scripts\scriptName
                    if (scriptPath == null)
                    {
                        var cand = System.IO.Path.Combine(baseDir, "scripts", scriptName);
                        if (File.Exists(cand)) scriptPath = cand;
                    }
                }
                catch { }

                if (scriptPath == null)
                {
                    TxtLogAppend("ERR: no se encontró el script de verificación. Rutas comprobadas: " + string.Join("; ", tried.Distinct()));
                    return;
                }

                var psi = new System.Diagnostics.ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -ManifestPath \"{_lastManifestPath}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var p = System.Diagnostics.Process.Start(psi);
                if (p is null)
                {
                    TxtLogAppend("Error: no se pudo iniciar PowerShell.");
                    return;
                }

                var outText = p.StandardOutput.ReadToEnd();
                var errText = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrWhiteSpace(outText)) TxtLogAppend(outText.Trim());
                if (!string.IsNullOrWhiteSpace(errText)) TxtLogAppend("ERR: " + errText.Trim());
                TxtLogAppend($"Comando terminado (exit {p.ExitCode})");
                try
                {
                    var ev = new ClinicaLongevidadApp.Models.AuditoriaEvento
                    {
                        Accion = "Verificar manifiesto (script)",
                        Modulo = "AuditorMenu",
                        UsuarioAdmin = Environment.UserName ?? string.Empty,
                        Resultado = p.ExitCode == 0,
                        FechaHora = DateTime.UtcNow,
                        Detalles = _lastManifestPath ?? scriptPath ?? string.Empty
                    };
                    App.AuditoriaService?.RegistrarEvento(ev);
                }
                catch { }
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error verificando manifiesto: " + ex.Message);
            }
        }

        private void BtnUnpack_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_selectedZipPath))
                {
                    TxtLogAppend("Seleccione primero el ZIP.");
                    return;
                }

                var dest = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "audit_check");
                if (Directory.Exists(dest)) Directory.Delete(dest, true);
                System.IO.Compression.ZipFile.ExtractToDirectory(_selectedZipPath, dest);
                TxtLogAppend("ZIP descomprimido en: " + dest);
                // Abrir carpeta
                try { OpenFileInExplorer(dest); } catch { }
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error descomprimiendo: " + ex.Message);
            }
            try
            {
                var ev = new ClinicaLongevidadApp.Models.AuditoriaEvento
                {
                    Accion = "Descomprimir ZIP",
                    Modulo = "AuditorMenu",
                    UsuarioAdmin = Environment.UserName ?? string.Empty,
                    Resultado = true,
                    FechaHora = DateTime.UtcNow,
                    Detalles = _selectedZipPath ?? string.Empty
                };
                App.AuditoriaService?.RegistrarEvento(ev);
            }
            catch { }
        }

        private void BtnGenDiag_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? conn = null;
                try { conn = Application.Current.Properties["AuditConnectionString"] as string; } catch { }
                conn ??= Environment.GetEnvironmentVariable("AUDIT_DB") ?? string.Empty;

                if (string.IsNullOrWhiteSpace(conn) && App.AuditoriaService is null)
                {
                    TxtLogAppend("No hay cadena de conexión de auditoría disponible.");
                    return;
                }

                var svc = App.AuditoriaService ?? new Services.AuditoriaService(conn ?? string.Empty);

                Task.Run(() =>
                {
                    try
                    {
                        string? quick = null;
                        string? report = null;
                        try { quick = svc.GenerateQuickDiagnostics(); } catch (Exception ex) { quick = "ERR: " + ex.Message; }
                        try { report = svc.GenerateIntegrityDiagnosticReport(); } catch (Exception ex) { report = "ERR: " + ex.Message; }

                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            if (!string.IsNullOrWhiteSpace(quick)) TxtLogAppend("Diagnóstico rápido: " + quick);
                            if (!string.IsNullOrWhiteSpace(report)) TxtLogAppend("Informe de integridad: " + report);
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current?.Dispatcher?.Invoke(() => TxtLogAppend("Error generando diagnósticos: " + ex.Message));
                    }
                });

                TxtLogAppend("Generando diagnósticos en background...");
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error inicializando diagnósticos: " + ex.Message);
            }
        }

        private void BtnPrepareDeliver_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Resumen de verificación - " + DateTime.UtcNow.ToString("u"));
                // include auditor name and local timestamp if provided
                string auditor = string.Empty;
                try { auditor = (this.FindName("TxtAuditorName") as System.Windows.Controls.TextBox)?.Text ?? string.Empty; } catch { }
                if (!string.IsNullOrWhiteSpace(auditor))
                {
                    sb.AppendLine("Auditor: " + auditor.Trim());
                }
                sb.AppendLine("Fecha (local): " + DateTime.Now.ToString("u"));
                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(_selectedZipPath))
                {
                    sb.AppendLine("ZIP: " + _selectedZipPath);
                    sb.AppendLine("SHA256: " + (_lastHash == string.Empty ? "NO_CALCULADO" : _lastHash));
                }
                else
                {
                    sb.AppendLine("ZIP: NO SELECCIONADO");
                }

                sb.AppendLine();
                sb.AppendLine("Manifiesto: " + (_lastManifestPath ?? "NO SELECCIONADO"));
                sb.AppendLine();
                sb.AppendLine("Instrucciones de entrega:");

                if (RbGitHub.IsChecked == true)
                {
                    sb.AppendLine("- GitHub Release: usar `gh release upload <tag> <file>` (clobber si procede).");
                    sb.AppendLine("  Ejemplo: gh release upload audit-rewrite-8684b20 \"" + (_selectedZipPath ?? "<RUTA_ZIP>") + "\" --repo <owner/repo> --clobber");
                }
                else if (RbS3.IsChecked == true)
                {
                    sb.AppendLine("- S3 (ejemplo con AWS CLI):");
                    sb.AppendLine("  aws s3 cp \"" + (_selectedZipPath ?? "<RUTA_ZIP>") + "\" s3://bucket/path/ --acl private");
                    sb.AppendLine("  O generar presigned URL (aws s3 presign s3://bucket/path/file.zip --expires-in 86400)");
                }
                else if (RbAzure.IsChecked == true)
                {
                    sb.AppendLine("- Azure Blob (ejemplo az cli):");
                    sb.AppendLine("  az storage blob upload --container-name <container> --file \"" + (_selectedZipPath ?? "<RUTA_ZIP>") + "\" --name <nombreBlob> --connection-string \"<CONNSTR>\"");
                    sb.AppendLine("  O generar SAS para descarga.");
                }
                else if (RbSftp.IsChecked == true)
                {
                    sb.AppendLine("- SFTP / SCP (ejemplo scp):");
                    sb.AppendLine("  scp \"" + (_selectedZipPath ?? "<RUTA_ZIP>") + "\" auditor@host:/path/to/store/");
                }

                TxtLog.Text = sb.ToString();
                TxtLogAppend("Impreso preparado.");
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error preparando impreso: " + ex.Message);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var outPath = System.IO.Path.Combine(ClinicaLongevidadApp.Services.AppPaths.AuditArtifactsDir, "audit_delivery_summary_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllText(outPath, TxtLog.Text ?? string.Empty, Encoding.UTF8);
                TxtLogAppend("Impreso guardado en: " + outPath);
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer", "/select,\"" + outPath + "\"") { UseShellExecute = true }); } catch { }
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error guardando impreso: " + ex.Message);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnClearScreen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear manifest grid
                var dg = this.FindName("DgManifest") as System.Windows.Controls.DataGrid;
                if (dg != null)
                {
                    dg.ItemsSource = null;
                    dg.Items.Clear();
                    dg.Items.Refresh();
                }

                // Clear detected zips
                try { LstDetectedZips.Items.Clear(); } catch { }

                // Clear log
                try { TxtLog.Clear(); } catch { }

                // Reset selected paths and hashes
                _selectedZipPath = null;
                _lastManifestPath = null;
                _lastHash = string.Empty;

                // Clear UI fields
                try
                {
                    var pbBox = this.FindName("PwdHmacKey") as System.Windows.Controls.PasswordBox;
                    if (pbBox != null) pbBox.Password = string.Empty;
                }
                catch { }
                try
                {
                    var txt = this.FindName("TxtAuditorName") as System.Windows.Controls.TextBox;
                    if (txt != null) txt.Text = string.Empty;
                }
                catch { }
                _hmacKeyAutoLoaded = false;

                // Attempt to remove known temporary folders created by the app
                try
                {
                    var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "audit_check");
                    if (System.IO.Directory.Exists(tmp))
                    {
                        System.IO.Directory.Delete(tmp, true);
                        TxtLogAppend("Ficheros temporales eliminados: " + tmp);
                    }
                }
                catch (Exception ex)
                {
                    TxtLogAppend("No se pudieron eliminar ficheros temporales: " + ex.Message);
                }

                TxtLogAppend("Pantalla limpiada.");
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error limpiando pantalla: " + ex.Message);
            }
        }

        // Ordena un fichero de log de auditoría por fecha/hora (descendente) y sobreescribe el fichero
        // Crea una copia de seguridad con la extensión .bak antes de sobrescribir.
        private void BtnSortAuditLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog { Filter = "Audit log|*.log;*.txt|All files|*.*", Title = "Seleccionar fichero de audit log" };
                if (dlg.ShowDialog(this) != true) return;

                var path = dlg.FileName;
                TxtLogAppend("Ordenando fichero de auditoría: " + path);

                var lines = File.ReadAllLines(path, Encoding.UTF8)
                                .Where(l => !string.IsNullOrWhiteSpace(l))
                                .ToList();

                var rx = new Regex("(\\d{1,2}\\/\\d{1,2}\\/\\d{4})\\s+(\\d{1,2}:\\d{2}:\\d{2})");

                #pragma warning disable CS8600
                DateTime? ExtractTimestamp(string line)
                {
                    if (string.IsNullOrWhiteSpace(line)) return null;

                    // First try: split by whitespace and parse first two tokens (date + time)
                    var parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var p0 = parts.Length > 0 ? parts[0] ?? string.Empty : string.Empty;
                        var p1 = parts.Length > 1 ? parts[1] ?? string.Empty : string.Empty;
                        var candidate = (p0 + " " + p1).Trim();
                        var fmts = new[] { "dd/MM/yyyy H:mm:ss", "d/M/yyyy H:mm:ss", "dd/MM/yyyy HH:mm:ss", "d/M/yyyy HH:mm:ss" };
                        if (DateTime.TryParseExact(candidate, fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                            return dt;
                    }

                    // Second try: regex anywhere in line
                    var m = rx.Match(line);
                    if (m.Success)
                    {
                        var dtStr = m.Groups[1].Value + " " + m.Groups[2].Value;
                        var fmts = new[] { "dd/MM/yyyy H:mm:ss", "d/M/yyyy H:mm:ss", "dd/MM/yyyy HH:mm:ss", "d/M/yyyy HH:mm:ss" };
                        if (DateTime.TryParseExact(dtStr, fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2))
                            return dt2;
                    }

                    return null;
                }
                #pragma warning restore CS8600

                var sorted = lines.OrderByDescending(l => ExtractTimestamp(l) ?? DateTime.MinValue).ToArray();

                var bak = path + ".bak";
                File.Copy(path, bak, true);
                File.WriteAllLines(path, sorted, Encoding.UTF8);

                TxtLogAppend($"Fichero ordenado y sobrescrito. Copia de seguridad: {bak}");
            }
            catch (Exception ex)
            {
                TxtLogAppend("Error ordenando audit log: " + ex.Message);
            }
        }

        private void TxtLogAppend(string text)
        {
            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            TxtLog.ScrollToEnd();
        }
    }
}
