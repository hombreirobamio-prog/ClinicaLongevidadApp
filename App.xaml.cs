using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.ViewModels;
using ClinicaLongevidadApp.Views;

namespace ClinicaLongevidadApp
{
    public partial class App : Application
    {
        private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan InactivityCheckInterval = TimeSpan.FromSeconds(15);
        private static System.Threading.Timer? _inactivityTimer;
        private static DateTime _lastUserActivityUtc = DateTime.UtcNow;
        private static bool _sessionLocked;

        public static AuditoriaService? AuditoriaService { get; private set; }

        public static DashboardViewModel? DashboardViewModel { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Use centralized app folder for persistent data
            string dbPath = Path.Combine(ClinicaLongevidadApp.Services.AppPaths.BaseDir, "ClinicaLongevidad.db");
            try
            {
                var newDbDir = ClinicaLongevidadApp.Services.AppPaths.BaseDir;
                var newDbPath = dbPath;

                // Legacy location (prior to centralization)
                var legacyDbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicaLongevidad.db");

                // Migrate only into an absent central database. Never replace an
                // existing active database based on file timestamps.
                if (System.IO.File.Exists(legacyDbPath) && !System.IO.File.Exists(newDbPath))
                {
                    try { System.IO.Directory.CreateDirectory(newDbDir); } catch { }

                    try
                    {
                        System.IO.File.Copy(legacyDbPath, newDbPath, overwrite: false);
                        try { AuditLogHelper.Info("App", $"Migrated legacy audit DB from {legacyDbPath} to {newDbPath}"); } catch { }
                    }
                    catch (Exception ex)
                    {
                        try { AuditLogHelper.Warning("App", "Failed migrating legacy DB: " + ex.Message); } catch { }
                    }
                }
            }
            catch { }

            string connectionString = $"Data Source={dbPath}";

            // Configure key provider: prefer Azure Key Vault if configured
            IKeyProvider keyProvider;
            var usingAzureKeyVault = false;
            string? vaultUri = Environment.GetEnvironmentVariable("KEYVAULT_URI");
            if (!string.IsNullOrWhiteSpace(vaultUri))
            {
                try
                {
                    keyProvider = new AzureKeyVaultKeyProvider();
                    usingAzureKeyVault = true;
                }
                catch
                {
                    // fallback
                    keyProvider = new LocalKeyProvider();
                }
            }
            else
            {
                keyProvider = new LocalKeyProvider();
            }

            // Enforce Key Vault before constructing any audit service that could
            // otherwise fall back to the local provider.
            var requireKv = string.Equals(Environment.GetEnvironmentVariable("REQUIRE_KEYVAULT"), "1", StringComparison.OrdinalIgnoreCase);
            var envName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
            var isProdEnv = string.Equals(envName, "Production", StringComparison.OrdinalIgnoreCase);
            if (requireKv || isProdEnv)
            {
                var hmacSecretName = Environment.GetEnvironmentVariable("AUDIT_HMAC_SECRET_NAME");
                var encSecretName = Environment.GetEnvironmentVariable("AUDIT_ENC_SECRET_NAME");
                var keyVaultReady = usingAzureKeyVault
                    && !string.IsNullOrWhiteSpace(hmacSecretName)
                    && !string.IsNullOrWhiteSpace(encSecretName)
                    && keyProvider.GetHmacKey() is { Length: >= 32 }
                    && !string.IsNullOrWhiteSpace(keyProvider.GetHmacKeyVersion())
                    && keyProvider.GetEncryptionKey() is { Length: 16 or 24 or 32 }
                    && !string.IsNullOrWhiteSpace(keyProvider.GetEncryptionKeyVersion());
                if (!keyVaultReady)
                {
                    var msg = "Key Vault is required in this environment but its audit secrets cannot be read with valid key material and versions. Aborting startup.";
                    try { MessageBox.Show(msg, "Configuration error", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
                    AuditLogHelper.Error("App", msg);
                    Shutdown();
                    return;
                }
            }

            // Optional exporters/forwarders
            IAuditExporter? exporter = null;
            IWebhookForwarder? forwarder = null;

            string? storageConfigured = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING") ?? Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URI");
            if (!string.IsNullOrWhiteSpace(storageConfigured))
            {
                try { exporter = new BlobAuditExporter(keyProvider); } catch { exporter = null; }
            }

            string? webhookUrl = Environment.GetEnvironmentVariable("AUDIT_WEBHOOK_URL");
            if (!string.IsNullOrWhiteSpace(webhookUrl))
            {
                try { forwarder = new WebhookForwarder(keyProvider); } catch { forwarder = null; }
            }

            try
            {
                AuditoriaService = new AuditoriaService(connectionString, keyProvider, exporter, forwarder);
                if (AuditoriaService != null && !AuditoriaService.IsInitialized)
                {
                    AuditLogHelper.Error("App", "AuditoriaService failed to initialize correctly; auditing disabled for this session.");
                    AuditoriaService = null; // avoid using a partially-initialized instance
                }
            }
            catch (Exception ex)
            {
                // Fail-safe: do not allow exceptions during audit service construction to stop the app.
                try { AuditLogHelper.Error("App", "Exception while creating AuditoriaService", ex); } catch { }
                AuditoriaService = null;
            }

            // Start persistent forward queue worker to guarantee forwarding durability
            // This is opt-in: enable by setting AUDIT_FORWARD_ENABLED=1 in the environment.
            try
            {
                var forwardEnabled = string.Equals(Environment.GetEnvironmentVariable("AUDIT_FORWARD_ENABLED"), "1", StringComparison.OrdinalIgnoreCase);
                AuditLogHelper.Info("App", $"AUDIT_FORWARD_ENABLED={(forwardEnabled ? "1" : "0")}");

                if (!forwardEnabled)
                {
                    AuditLogHelper.Info("App", "Audit forward queue worker disabled by AUDIT_FORWARD_ENABLED flag");
                }
                else if (AuditoriaService == null || AuditoriaService.IsInitialized == false)
                {
                    AuditLogHelper.Warning("App", "Audit forward queue worker not started because AuditoriaService is not available or failed to initialize");
                }
                else if (forwarder == null && exporter == null)
                {
                    AuditLogHelper.Info("App", "Audit forward queue worker not started because no forwarder or exporter is configured (AUDIT_WEBHOOK_URL or storage connection missing)");
                }
                else
                {
                    var forwardQueueWorker = new AuditForwardQueueWorker(connectionString, forwarder, exporter, 30);
                    Current.Properties["AuditForwardQueueWorker"] = forwardQueueWorker;
                    AuditLogHelper.Info("App", "Audit forward queue worker started (opt-in enabled and forwarder/exporter available)");
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: log and continue
                AuditLogHelper.Warning("App", "Failed to start AuditForwardQueueWorker: " + ex.Message);
            }
            // Store connection string for tools and UI backup service
            Current.Properties["AuditConnectionString"] = connectionString;

            // Configure key rotation provider and service: prefer Azure Key Vault when available
            IKeyRotationProvider? rotationProvider = null;
            if (!string.IsNullOrWhiteSpace(vaultUri))
            {
                try
                {
                    rotationProvider = new AzureKeyRotationProvider();
                }
                catch
                {
                    if (requireKv || isProdEnv)
                    {
                        var msg = "No se pudo inicializar el proveedor de rotación de Key Vault en un entorno que lo exige. Abortando inicio.";
                        try { MessageBox.Show(msg, "Configuration error", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
                        AuditLogHelper.Error("App", msg);
                        Shutdown();
                        return;
                    }

                    rotationProvider = new LocalKeyRotationProvider();
                }
            }
            else
            {
                rotationProvider = new LocalKeyRotationProvider();
            }

            // Create key rotation orchestrator and keep it available in Application properties
            var keyRotationService = new KeyRotationService(rotationProvider, AuditoriaService);
            Current.Properties["KeyRotationService"] = keyRotationService;

            // Start integrity worker (opt-in). Enable with AUDIT_INTEGRITY_ENABLED=1
            try
            {
                var integrityEnabled = string.Equals(Environment.GetEnvironmentVariable("AUDIT_INTEGRITY_ENABLED"), "1", StringComparison.OrdinalIgnoreCase);
                AuditLogHelper.Info("App", $"AUDIT_INTEGRITY_ENABLED={(integrityEnabled ? "1" : "0")}");

                if (!integrityEnabled)
                {
                    AuditLogHelper.Info("App", "AuditoriaIntegrityWorker disabled by AUDIT_INTEGRITY_ENABLED flag");
                }
                else if (AuditoriaService == null || !AuditoriaService.IsInitialized)
                {
                    AuditLogHelper.Warning("App", "AuditoriaIntegrityWorker not started because AuditoriaService is not available or failed to initialize");
                }
                else
                {
                    var integrityWorker = new AuditoriaIntegrityWorker(AuditoriaService, TimeSpan.FromMinutes(60));

                    // Subscribe to integrity failure events to escalate (webhook/export/log).
                    integrityWorker.OnIntegrityFailure += async (errors) =>
                    {
                        try
                        {
                            string eventId = Guid.NewGuid().ToString("N");
                            var payloadObj = new
                            {
                                EventId = eventId,
                                TimestampUtc = DateTime.UtcNow,
                                Machine = Environment.MachineName,
                                AppVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty,
                                HmacKeyVersion = keyProvider?.GetHmacKeyVersion() ?? string.Empty,
                                EncKeyVersion = keyProvider?.GetEncryptionKeyVersion() ?? string.Empty,
                                Errors = errors
                            };

                            string json = JsonSerializer.Serialize(payloadObj);

                            // Compute signature using key provider if available
                            string signature = string.Empty;
                            try
                            {
                                var key = keyProvider?.GetHmacKey();
                                if (key != null && key.Length > 0)
                                {
                                    using var hmac = new HMACSHA256(key);
                                    var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(json ?? string.Empty));
                                    signature = Convert.ToHexString(sig);
                                }
                            }
                            catch (Exception ex)
                            {
                                AuditLogHelper.Warning("App", "Failed to compute HMAC for integrity failure payload: " + ex.Message);
                            }

                            // Log a concise error (avoid dumping sensitive payload in logs)
                            AuditLogHelper.Error("App", $"Audit integrity failure detected ({errors.Count}) - eventId={eventId}");

                            // Forward to webhook if available
                            if (forwarder != null)
                            {
                                try { await forwarder.ForwardEventAsync(json!, signature); } catch (Exception ex) { AuditLogHelper.Error("App", "Error forwarding integrity alert", ex); }
                            }

                            // Export to blob storage if configured
                            if (exporter != null)
                            {
                                try { await exporter.ExportEventAsync(eventId, json!, signature); } catch (Exception ex) { AuditLogHelper.Error("App", "Error exporting integrity alert", ex); }
                            }
                        }
                        catch (Exception ex)
                        {
                            AuditLogHelper.Error("App", "Error handling integrity failure", ex);
                        }
                    };

                    integrityWorker.Start();
                    // store in App properties for shutdown
                    Current.Properties["AuditoriaIntegrityWorker"] = integrityWorker;
                    AuditLogHelper.Info("App", "AuditoriaIntegrityWorker started (opt-in enabled)");
                }
            }
            catch (Exception ex)
            {
                // Log worker start failures but keep app running
                AuditLogHelper.Error("App", "Failed to start AuditoriaIntegrityWorker", ex);
            }

            DashboardViewModel = new DashboardViewModel();

            InputManager.Current.PreProcessInput += OnPreProcessInput;
            StartInactivityMonitoring();

            var mainWindow = new MainWindow();

            MainWindow = mainWindow;

            mainWindow.Show();

            // Diagnostics do not grant a session or administrative permissions.
            // so the auditor can immediately obtain quick diagnostics and an integrity report.
            try
            {
                if (string.Equals(Environment.GetEnvironmentVariable("AUDIT_RUN_DIAGNOSTICS"), "1", StringComparison.OrdinalIgnoreCase))
                {
                    // Run diagnostics in background to avoid blocking UI startup
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            string? quickPath = null;
                            string? reportPath = null;
                            try { quickPath = AuditoriaService?.GenerateQuickDiagnostics(); } catch { }
                            try { reportPath = AuditoriaService?.GenerateIntegrityDiagnosticReport(); } catch { }

                            try { AuditLogHelper.Info("App", "Audit self-check completed. quick=" + (quickPath ?? "") + " report=" + (reportPath ?? "")); } catch { }

                            // Notify the UI with locations (marshal to UI thread)
                            try
                            {
                                Application.Current?.Dispatcher?.Invoke(() =>
                                {
                                    try
                                    {
                                        var sb = new System.Text.StringBuilder();
                                        sb.AppendLine("Audit self-check completed.");
                                        if (!string.IsNullOrWhiteSpace(quickPath)) sb.AppendLine("Quick diagnostics dir: " + quickPath);
                                        if (!string.IsNullOrWhiteSpace(reportPath)) sb.AppendLine("Integrity report: " + reportPath);
                                        if (string.IsNullOrWhiteSpace(quickPath) && string.IsNullOrWhiteSpace(reportPath)) sb.AppendLine("No diagnostics were generated (no integrity errors or service unavailable).");
                                        try { MessageBox.Show(sb.ToString(), "Audit self-check", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
                                    }
                                    catch { }
                                });
                            }
                            catch { }
                        }
                        catch { }
                    });
                }
            }
            catch { }
        }

        public static void CerrarSesion()
        {
            string? usuarioActual = Sesion.UsuarioActual;

            try
            {
                AuditoriaService?.RegistrarEvento(new AuditoriaEvento
                {
                    UsuarioAdmin = usuarioActual ?? "Sistema",
                    Accion = "Sesion.Cerrar",
                    Modulo = "Sesión",
                    UsuarioAfectado = usuarioActual ?? string.Empty,
                    Resultado = true,
                    FechaHora = DateTime.Now,
                    Tipo = "Sesion",
                    // Provide explicit metadata so DB columns are populated for auditors
                    Rol = Sesion.RolActual ?? string.Empty,
                    Area = Sesion.AreaActual ?? string.Empty,
                    SesionId = Sesion.UsuarioActual ?? Guid.NewGuid().ToString("N"),
                    Equipo = Environment.MachineName ?? string.Empty,
                    VersionApp = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty,
                    Detalles = AuditoriaDetallesHelper.CrearJson(
                        ("Usuario", usuarioActual ?? string.Empty),
                        ("Area", Sesion.AreaActual ?? string.Empty))
                });
            }
            catch
            {
                // No interrumpir el cierre de sesión si falla la auditoría.
            }
            finally
            {
                ResetInactivityTracking();
                Sesion.Limpiar();
                DashboardViewModel = null;

                if (Current?.MainWindow is not null)
                {
                    Current.MainWindow.Content = new WelcomeView();
                }
            }
        }

        private static void StartInactivityMonitoring()
        {
            _lastUserActivityUtc = DateTime.UtcNow;
            _inactivityTimer?.Dispose();
            _inactivityTimer = new System.Threading.Timer(
                _ => CheckInactivity(),
                null,
                InactivityCheckInterval,
                InactivityCheckInterval);
        }

        private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            if (_sessionLocked)
            {
                return;
            }

            if (Sesion.UsuarioActual is null)
            {
                return;
            }

            _lastUserActivityUtc = DateTime.UtcNow;
        }

        private static void CheckInactivity()
        {
            if (_sessionLocked || Sesion.UsuarioActual is null)
            {
                return;
            }

            TimeSpan idleTime = GetIdleTime();
            TimeSpan appIdle = DateTime.UtcNow - _lastUserActivityUtc;

            if (idleTime < InactivityTimeout && appIdle < InactivityTimeout)
            {
                return;
            }

            // Lock session logic omitted for brevity
        }

        private static void ResetInactivityTracking()
        {
            _lastUserActivityUtc = DateTime.UtcNow;
            _inactivityTimer?.Change(InactivityCheckInterval, InactivityCheckInterval);
            _sessionLocked = false;
        }

        private static TimeSpan GetIdleTime()
        {
            try
            {
                var info = new LASTINPUTINFO();
                info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(info);
                if (GetLastInputInfo(ref info))
                {
                    uint tick = (uint)Environment.TickCount;
                    uint idle = tick - info.dwTime;
                    return TimeSpan.FromMilliseconds(idle);
                }
            }
            catch
            {
                // best-effort: if we cannot query system idle, assume not idle
            }

            return TimeSpan.Zero;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    }
}
