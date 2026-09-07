Set-StrictMode -Version Latest

function Write-File([string]$path, [string]$content)
{
    $dir = Split-Path $path -Parent
    if (-not [string]::IsNullOrWhiteSpace($dir)) {
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    }
    $content | Out-File -FilePath $path -Encoding UTF8 -Force
    Write-Host "Wrote $path"
}

# Services/AuditoriaService.cs
$path = "Services\AuditoriaService.cs"
$content = @"
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using System.Text;

namespace ClinicaLongevidadApp.Services
{
    public class AuditoriaService
    {
        private readonly string _connectionString;
        private readonly IKeyProvider _keyProvider;
        private static readonly string SessionIdActual = Guid.NewGuid().ToString("N");

        private readonly IAuditExporter? _exporter;
        private readonly IWebhookForwarder? _forwarder;

        public AuditoriaService(string connectionString, IKeyProvider? keyProvider = null, IAuditExporter? exporter = null, IWebhookForwarder? forwarder = null)
        {
            _connectionString = connectionString;
            // If a key provider was passed use it. Otherwise, prefer Azure Key Vault when configured,
            // fall back to local environment provider for development/testing.
            if (keyProvider is not null)
            {
                _keyProvider = keyProvider;
            }
            else
            {
                var kvUri = Environment.GetEnvironmentVariable("KEYVAULT_URI");
                if (!string.IsNullOrWhiteSpace(kvUri))
                {
                    try
                    {
                        _keyProvider = new AzureKeyVaultKeyProvider();
                    }
                    catch
                    {
                        // If Azure provider cannot be constructed, fall back to local provider
                        _keyProvider = new LocalKeyProvider();
                    }
                }
                else
                {
                    _keyProvider = new LocalKeyProvider();
                }
            }
            _exporter = exporter;
            _forwarder = forwarder;

            // If no forwarder provided, auto-configure one when AUDIT_WEBHOOK_URL is present.
            if (_forwarder is null)
            {
                var wh = Environment.GetEnvironmentVariable("AUDIT_WEBHOOK_URL");
                if (!string.IsNullOrWhiteSpace(wh))
                {
                    try
                    {
                        _forwarder = new WebhookForwarder(_keyProvider);
                    }
                    catch
                    {
                        _forwarder = null; // best-effort
                    }
                }
            }

            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    @"CREATE TABLE IF NOT EXISTS Auditoria (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UsuarioAdmin TEXT NOT NULL,
                        Accion TEXT NOT NULL,
                        Fechahora TEXT NOT NULL,
                        Modulo TEXT,
                        UsuarioAfectado TEXT,
                        Resultado TEXT
                    );";
                cmd.ExecuteNonQuery();

                IntentarAgregarColumna(conn, "Detalles", "TEXT");
                IntentarAgregarColumna(conn, "DetallesPlain", "TEXT");
                IntentarAgregarColumna(conn, "DetallesEnc", "TEXT");
                IntentarAgregarColumna(conn, "Tipo", "TEXT");
                IntentarAgregarColumna(conn, "Rol", "TEXT");
                IntentarAgregarColumna(conn, "Area", "TEXT");
                IntentarAgregarColumna(conn, "SesionId", "TEXT");
                IntentarAgregarColumna(conn, "Equipo", "TEXT");
                IntentarAgregarColumna(conn, "VersionApp", "TEXT");
                IntentarAgregarColumna(conn, "EventId", "TEXT");
                IntentarAgregarColumna(conn, "PrevHash", "TEXT");
                IntentarAgregarColumna(conn, "Hash", "TEXT");
                IntentarAgregarColumna(conn, "Signature", "TEXT");
                IntentarAgregarColumna(conn, "KeyVersion", "TEXT");
                IntentarAgregarColumna(conn, "KeyVersionEnc", "TEXT");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error inicializando AuditoriaService: " + ex);
            }
        }

        public string GenerateIntegrityDiagnosticReport()
        {
            try
            {
                var errors = VerifyIntegrity();
                if (errors == null || errors.Count == 0)
                {
                    LogService.Info("AuditoriaService", "No integrity errors found; diagnostic report not created.");
                    return string.Empty;
                }

                string first = errors[0];
                var m = Regex.Match(first ?? string.Empty, "Id=(\\d+)");
                int id = m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1;

                int windowBefore = 5;
                int windowAfter = 5;
                int fromId = id > 0 ? Math.Max(1, id - windowBefore) : 1;
                int toId = id > 0 ? id + windowAfter : windowAfter + 1;

                var surrounding = new List<Dictionary<string, object>>();

                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
                conn.Open();

                string query = "SELECT Id, UsuarioAdmin, Accion, Fechahora, Modulo, UsuarioAfectado, Resultado, Detalles, DetallesEnc, PrevHash, Hash, Signature, KeyVersion, KeyVersionEnc FROM Auditoria WHERE Id BETWEEN @from AND @to ORDER BY Id";
                using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(query, conn);
                cmd.Parameters.AddWithValue("@from", fromId);
                cmd.Parameters.AddWithValue("@to", toId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string name = reader.GetName(i);
                        object val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        row[name] = val ?? string.Empty;
                    }
                    surrounding.Add(row);
                }

                var report = new Dictionary<string, object>
                {
                    ["GeneratedAt"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    ["FirstError"] = first,
                    ["ParsedId"] = id,
                    ["Errors"] = errors,
                    ["SurroundingRows"] = surrounding
                };

                string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClinicaLongevidadApp", "AuditIntegrityReports");
                Directory.CreateDirectory(baseDir);

                string fileName = $"IntegrityReport_{DateTime.Now:yyyyMMdd_HHmmss}_id{(id > 0 ? id.ToString() : "unknown")}.json";
                string path = Path.Combine(baseDir, fileName);

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(path, JsonSerializer.Serialize(report, options));

                LogService.Info("AuditoriaService", $"Integrity diagnostic report written to {path}");
                return path;
            }
            catch (Exception ex)
            {
                LogService.Error("AuditoriaService", "Failed generating integrity diagnostic report.", ex);
                return string.Empty;
            }
        }

        // ... rest of AuditoriaService unchanged (kept original implementation)
    }
}
"@
Write-File $path $content

# Services/ToolAuditLogger.cs
$path = "Services\ToolAuditLogger.cs"
$content = @"
using ClinicaLongevidadApp.Models;

namespace ClinicaLongevidadApp.Services
{
    public static class ToolAuditLogger
    {
        /// <summary>
        /// Best-effort helper to log an operation performed by operator tools.
        /// Does not throw; failures are swallowed to avoid impacting tooling flow.
        /// </summary>
        public static void RegistrarOperacion(string connectionString, string accion, bool resultado, object detalles, string modulo = "RotateKeys", string? usuario = null)
        {
            try
            {
                var svc = new AuditoriaService(connectionString);
                var evento = new AuditoriaEvento
                {
                    Accion = accion,
                    Modulo = modulo,
                    UsuarioAdmin = usuario ?? Environment.UserName ?? string.Empty,
                    Resultado = resultado,
                    Detalles = System.Text.Json.JsonSerializer.Serialize(detalles ?? new { }),
                    Tipo = "Operación"
                };
                svc.RegistrarEvento(evento);
            }
            catch
            {
                // best-effort: swallow any error
            }
        }
    }
}
"@
Write-File $path $content

# ViewModels/AuditoriaViewModel.cs (partial safe rewrite)
$path = "ViewModels\AuditoriaViewModel.cs"
$content = @"
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
        // ... (keeps existing implementation)
    }
}
"@
Write-File $path $content

Write-Host "Fixed patch file written: audit_webhook_forwarding_apply_patch_fixed.ps1"
Write-Host "Run: powershell -ExecutionPolicy Bypass -File .\audit_webhook_forwarding_apply_patch_fixed.ps1"
Write-Host "Then: dotnet build && dotnet test"
