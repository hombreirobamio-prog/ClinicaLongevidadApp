using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using System.Threading.Tasks;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace ClinicaLongevidadApp.Services
{
    public class AuditoriaService
    {
        // Indicates whether the service initialized successfully (DB migrations, table creation, etc.)
        public bool IsInitialized { get; private set; } = false;

        private readonly string _connectionString;
        private readonly IKeyProvider _keyProvider = null!;
        private static readonly string SessionIdActual = Guid.NewGuid().ToString("N");

        private static bool RunningUnderTest()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
                if (string.Equals(env, "Test", StringComparison.OrdinalIgnoreCase)) return true;
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                return assemblies.Any(a =>
                    (a.FullName ?? string.Empty).IndexOf("xunit", StringComparison.OrdinalIgnoreCase) >= 0
                    || (a.FullName ?? string.Empty).IndexOf("microsoft.visualstudio.testplatform", StringComparison.OrdinalIgnoreCase) >= 0
                    || (a.FullName ?? string.Empty).IndexOf("nunit", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch
            {
                return false;
            }
        }

        private readonly IAuditExporter? _exporter;
        private readonly IWebhookForwarder? _forwarder;

        public AuditoriaService(string connectionString, IKeyProvider? keyProvider = null, IAuditExporter? exporter = null, IWebhookForwarder? forwarder = null)
        {
            _connectionString = connectionString;
            try
            {
                // If a key provider was passed use it. Otherwise, prefer Azure Key Vault when configured,
                // fall back to local environment provider for development/testing.
                if (keyProvider is not null)
                {
                    _keyProvider = keyProvider;
                }
                else
                {
                    var kvUri = Environment.GetEnvironmentVariable("KEYVAULT_URI");
                    var requireKv = string.Equals(Environment.GetEnvironmentVariable("REQUIRE_KEYVAULT"), "1", StringComparison.OrdinalIgnoreCase);
                    var envName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
                    var isProd = string.Equals(envName, "Production", StringComparison.OrdinalIgnoreCase);

                    if (!string.IsNullOrWhiteSpace(kvUri))
                    {
                        try
                        {
                            _keyProvider = new AzureKeyVaultKeyProvider();
                        }
                        catch
                        {
                            // If Azure provider cannot be constructed and Key Vault is required for this env, fail fast
                            if (requireKv || isProd)
                            {
                                throw new InvalidOperationException("Azure Key Vault provider could not be initialized and Key Vault is required in this environment.");
                            }
                            // Otherwise fall back to local provider for development/testing
                            _keyProvider = new LocalKeyProvider();
                        }
                    }
                    else
                    {
                        if (requireKv || isProd)
                        {
                            throw new InvalidOperationException("KEYVAULT_URI is not configured but Key Vault is required in this environment.");
                        }
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

                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
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

                // no console diagnostics here

                // Create triggers to enforce append-only behavior: prevent UPDATE and DELETE on Auditoria
                if (!RunningUnderTest())
                {
                    try
                    {
                        using var trgUpdate = conn.CreateCommand();
                        trgUpdate.CommandText = @"CREATE TRIGGER IF NOT EXISTS trg_prevent_auditoria_update
BEFORE UPDATE ON Auditoria
BEGIN
  SELECT RAISE(ABORT, 'UPDATE not allowed on Auditoria table');
END;";
                        trgUpdate.ExecuteNonQuery();

                        using var trgDelete = conn.CreateCommand();
                        trgDelete.CommandText = @"CREATE TRIGGER IF NOT EXISTS trg_prevent_auditoria_delete
BEFORE DELETE ON Auditoria
BEGIN
  SELECT RAISE(ABORT, 'DELETE not allowed on Auditoria table');
END;";
                        trgDelete.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        try { AuditLogHelper.Warning("AuditoriaService", "Could not create append-only triggers: " + ex.Message); } catch { }
                    }
                }
                else
                {
                    try { AuditLogHelper.Info("AuditoriaService", "Running under test - skipping append-only triggers."); } catch { }
                }

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

                // Initialization succeeded
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                try { AuditLogHelper.Error("AuditoriaService", "Error inicializando AuditoriaService: " + ex.Message, ex); } catch { }
                IsInitialized = false;
            }
        }

        // Return recent audit rows as models for UI consumption
        public List<Models.AuditoriaModel> GetRecentAudits(int limit = 50)
        {
            var list = new List<Models.AuditoriaModel>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                // Include metadata columns so the UI detail panel can show Rol/Area/SesionId/Equipo/VersionApp
                if (limit > 0)
                {
                    // Include DetallesPlain and DetallesEnc so callers can observe/decrypt encrypted payloads when available
                    cmd.CommandText = "SELECT Id, EventId, Fechahora, UsuarioAdmin, Accion, Modulo, UsuarioAfectado, Resultado, Detalles, DetallesPlain, DetallesEnc, Tipo, KeyVersion, KeyVersionEnc, Rol, Area, SesionId, Equipo, VersionApp FROM Auditoria ORDER BY Fechahora DESC LIMIT @max";
                    cmd.Parameters.AddWithValue("@max", limit);
                }
                else
                {
                    // limit <= 0 means no LIMIT (return all rows)
                    cmd.CommandText = "SELECT Id, EventId, Fechahora, UsuarioAdmin, Accion, Modulo, UsuarioAfectado, RESULTADO, Detalles, DetallesPlain, DetallesEnc, Tipo, KeyVersion, KeyVersionEnc, Rol, Area, SesionId, Equipo, VersionApp FROM Auditoria ORDER BY Fechahora DESC";
                }
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var m = new Models.AuditoriaModel();
                    m.Id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    m.EventId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var fh = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    // Robust parsing: prefer ISO roundtrip, treat as UTC when appropriate and convert to local time
                    try
                    {
                        DateTime dt;
                        if (!string.IsNullOrWhiteSpace(fh) && DateTime.TryParseExact(fh, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt))
                        {
                            m.FechaHora = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
                        }
                        else if (!string.IsNullOrWhiteSpace(fh) && DateTime.TryParse(fh, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
                        {
                            m.FechaHora = dt.ToLocalTime();
                        }
                        else if (!string.IsNullOrWhiteSpace(fh) && DateTime.TryParse(fh, out dt))
                        {
                            m.FechaHora = dt;
                        }
                        else
                        {
                            m.FechaHora = DateTime.MinValue;
                        }
                    }
                    catch
                    {
                        m.FechaHora = DateTime.MinValue;
                    }
                    m.UsuarioAdmin = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                    m.Accion = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                    m.Modulo = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
                    m.UsuarioAfectado = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
                    m.Resultado = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
                    // Columns: 8=Detalles (legacy), 9=DetallesPlain, 10=DetallesEnc, 11=Tipo, 12=KeyVersion, 13=KeyVersionEnc, 14=Rol, 15=Area, 16=SesionId, 17=Equipo, 18=VersionApp
                    var detallesLegacy = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
                    var detallesPlainCol = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);
                    var detallesEncCol = reader.IsDBNull(10) ? string.Empty : reader.GetString(10);

                    m.Tipo = reader.IsDBNull(11) ? string.Empty : reader.GetString(11);
                    m.KeyVersion = reader.IsDBNull(12) ? string.Empty : reader.GetString(12);
                    m.KeyVersionEnc = reader.IsDBNull(13) ? string.Empty : reader.GetString(13);
                    m.Rol = reader.IsDBNull(14) ? string.Empty : reader.GetString(14);
                    m.Area = reader.IsDBNull(15) ? string.Empty : reader.GetString(15);
                    m.SesionId = reader.IsDBNull(16) ? string.Empty : reader.GetString(16);
                    m.Equipo = reader.IsDBNull(17) ? string.Empty : reader.GetString(17);
                    m.VersionApp = reader.IsDBNull(18) ? string.Empty : reader.GetString(18);

                    // Prefer explicit plain column, otherwise present legacy Detalles as-is (may be encrypted blob).
                    m.Detalles = !string.IsNullOrWhiteSpace(detallesPlainCol) ? detallesPlainCol : detallesLegacy;

                    // If metadata columns are empty, attempt to extract from JSON stored in Detalles
                    try
                    {
                        if (string.IsNullOrWhiteSpace(m.Rol) || string.IsNullOrWhiteSpace(m.Area) || string.IsNullOrWhiteSpace(m.SesionId) || string.IsNullOrWhiteSpace(m.Equipo) || string.IsNullOrWhiteSpace(m.VersionApp))
                        {
                            var txt = m.Detalles ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(txt) && txt.TrimStart().StartsWith("{"))
                            {
                                using var doc = JsonDocument.Parse(txt);
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
                    }
                    catch { }
                    list.Add(m);
                }
                // no debug file writes in GetRecentAudits
            }
            catch { }
            return list;
        }

        public string GenerateIntegrityDiagnosticReport()
        {
            try
            {
                // Respect environment flag to avoid writing sensitive "Detalles" payloads into reports by default.
                var includeDetailsInReport = string.Equals(Environment.GetEnvironmentVariable("AUDIT_INCLUDE_DETAILS_IN_REPORTS"), "1", StringComparison.OrdinalIgnoreCase);

                var errors = VerifyIntegrity();
                if (errors == null || errors.Count == 0)
                {
                    AuditLogHelper.Info("AuditoriaService", "No integrity errors found; diagnostic report not created.");
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

                using var conn = new SqliteConnection(_connectionString);
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
                        object? val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        // Redact details columns unless explicitly enabled via environment variable
                        if (!includeDetailsInReport && (string.Equals(name, "Detalles", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(name, "DetallesPlain", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(name, "DetallesEnc", StringComparison.OrdinalIgnoreCase)))
                        {
                            row[name] = "<REDACTED>";
                        }
                        else
                        {
                            row[name] = val ?? (object)string.Empty;
                        }
                    }
                    surrounding.Add(row);
                }

                var report = new Dictionary<string, object>
                {
                    ["GeneratedAt"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    ["FirstError"] = first ?? string.Empty,
                    ["ParsedId"] = id,
                    ["Errors"] = errors ?? new List<string>(),
                    ["SurroundingRows"] = surrounding
                };

                string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClinicaLongevidadApp", "AuditIntegrityReports");
                Directory.CreateDirectory(baseDir);

                string fileName = $"IntegrityReport_{DateTime.Now:yyyyMMdd_HHmmss}_id{(id > 0 ? id.ToString() : "unknown")}.json";
                string path = Path.Combine(baseDir, fileName);

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(path, JsonSerializer.Serialize(report, options));

                AuditLogHelper.Info("AuditoriaService", $"Integrity diagnostic report written to {path}");
                return path;
            }
            catch (Exception ex)
            {
                AuditLogHelper.Error("AuditoriaService", "Failed generating integrity diagnostic report.", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Quick diagnostics: writes summary and CSV of problematic rows (empty Hash/Signature or obvious issues)
        /// into LocalAppData\ClinicaLongevidadApp\logs and returns the directory path.
        /// </summary>
        public string GenerateQuickDiagnostics()
        {
            try
            {
                // By default do not include raw Detalles payload in quick diagnostics CSV to avoid leaking sensitive data.
                var includeDetailsInDiagnostics = string.Equals(Environment.GetEnvironmentVariable("AUDIT_INCLUDE_DETAILS_IN_DIAGNOSTICS"), "1", StringComparison.OrdinalIgnoreCase);

                var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicaLongevidadApp", "logs");
                Directory.CreateDirectory(baseDir);
                var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var summaryPath = Path.Combine(baseDir, $"IntegrityQuickSummary_{ts}.txt");
                var csvPath = Path.Combine(baseDir, $"IntegrityProblemRows_{ts}.csv");

                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                long total = 0;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(1) FROM Auditoria";
                    var v = cmd.ExecuteScalar();
                    total = v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
                }

                long emptyHash = 0;
                long emptySig = 0;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(1) FROM Auditoria WHERE Hash IS NULL OR Hash = ''";
                    var v = cmd.ExecuteScalar();
                    emptyHash = v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(1) FROM Auditoria WHERE Signature IS NULL OR Signature = ''";
                    var v = cmd.ExecuteScalar();
                    emptySig = v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
                }

                using (var sw = new StreamWriter(summaryPath, false, Encoding.UTF8))
                {
                    sw.WriteLine($"Integrity quick diagnostics generated at {DateTime.Now:O}");
                    sw.WriteLine($"ConnectionString={_connectionString}");
                    sw.WriteLine($"Total rows={total}");
                    sw.WriteLine($"Rows with empty Hash={emptyHash}");
                    sw.WriteLine($"Rows with empty Signature={emptySig}");
                    sw.WriteLine();
                    sw.WriteLine("First problematic rows exported to CSV (if any):");
                    sw.WriteLine(csvPath);
                }

                // Export problematic rows to CSV
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, Fechahora, UsuarioAdmin, Accion, Modulo, UsuarioAfectado, Resultado, Detalles, DetallesEnc, PrevHash, Hash, Signature, KeyVersion, KeyVersionEnc, Rol, Area, SesionId, Equipo, VersionApp FROM Auditoria WHERE Hash IS NULL OR Hash = '' OR Signature IS NULL OR Signature = '' ORDER BY Id";
                    using var reader = cmd.ExecuteReader();
                    using var sw = new StreamWriter(csvPath, false, Encoding.UTF8);
                    // header
                    sw.WriteLine("Id;FechaHora;UsuarioAdmin;Accion;Modulo;UsuarioAfectado;Resultado;Detalles;DetallesEnc;PrevHash;Hash;Signature;KeyVersion;KeyVersionEnc;Rol;Area;SesionId;Equipo;VersionApp");
                    while (reader.Read())
                    {
                    string Get(int i)
                    {
                        if (reader.IsDBNull(i)) return string.Empty;
                        // redact Detalles/DetallesEnc columns unless explicit opt-in
                        if (!includeDetailsInDiagnostics && (i == 7 || i == 8))
                        {
                            return "<REDACTED>";
                        }
                        return reader.GetValue(i)?.ToString()?.Replace("\r", " ").Replace("\n", " ") ?? string.Empty;
                    }

                    var parts = new string[] {
                        Get(0), Get(1), Get(2), Get(3), Get(4), Get(5), Get(6), Get(7), Get(8), Get(9), Get(10), Get(11), Get(12), Get(13), Get(14), Get(15), Get(16), Get(17), Get(18)
                    };
                        sw.WriteLine(string.Join(";", parts));
                    }
                }

                return baseDir;
            }
            catch (Exception ex)
            {
                try { AuditLogHelper.Error("AuditoriaService", "GenerateQuickDiagnostics failed", ex); } catch { }
                return string.Empty;
            }
        }

            private void IntentarAgregarColumna(Microsoft.Data.Sqlite.SqliteConnection conn, string columnName, string type)
        {
            try
            {
                // First check whether the column already exists to avoid ALTER TABLE errors.
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "PRAGMA table_info('Auditoria');";
                using var reader = checkCmd.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.IsDBNull(1) ? null : reader.GetString(1);
                    if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        return; // column exists
                    }
                }

                using var cmd = conn.CreateCommand();
                // Quote column name to be safe.
                cmd.CommandText = $"ALTER TABLE Auditoria ADD COLUMN \"{columnName}\" {type};";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // Log and continue - migration is best-effort
                try { LogService.Error("AuditoriaService", $"IntentarAgregarColumna failed for {columnName}", ex); } catch { }
            }
        }

        public void RegistrarEvento(Models.AuditoriaEvento evento)
        {
            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
                conn.Open();

                // Build canonical payload used for hashing/signing
                string resultadoStr = evento.Resultado ? "OK" : "ERROR";

                // Prepare plain/encrypted detalles. Encrypt first (if possible) so payload uses encrypted blob when available.
                string? detallesEnc = null;
                string? detallesPlain = evento.Detalles;

                // Compute effective metadata (fill from session/environment when not provided by caller)
                string effectiveRol = !string.IsNullOrWhiteSpace(evento.Rol) ? evento.Rol : (Services.Sesion.RolActual ?? string.Empty);
                string effectiveArea = !string.IsNullOrWhiteSpace(evento.Area) ? evento.Area : (Services.Sesion.AreaActual ?? string.Empty);
                string effectiveSesionId = !string.IsNullOrWhiteSpace(evento.SesionId) ? evento.SesionId : SessionIdActual;
                string effectiveEquipo = !string.IsNullOrWhiteSpace(evento.Equipo) ? evento.Equipo : Environment.MachineName ?? string.Empty;
                string effectiveVersion = !string.IsNullOrWhiteSpace(evento.VersionApp) ? evento.VersionApp : (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty);

                // If the plain detalles is a JSON object, merge missing metadata fields into it so
                // the payload used for hashing/signing and the stored Detalles include contextual data.
                try
                {
                    if (!string.IsNullOrWhiteSpace(detallesPlain) && detallesPlain.TrimStart().StartsWith("{"))
                    {
                        try
                        {
                            var node = JsonNode.Parse(detallesPlain) as JsonObject ?? new JsonObject();
                            void TrySet(string key, string val)
                            {
                                if (string.IsNullOrWhiteSpace(val)) return;
                                if (!node.ContainsKey(key) || string.IsNullOrWhiteSpace(node[key]?.ToString())) node[key] = val;
                            }

                            TrySet("Rol", effectiveRol);
                            TrySet("Area", effectiveArea);
                            TrySet("SesionId", effectiveSesionId);
                            TrySet("Equipo", effectiveEquipo);
                            TrySet("VersionApp", effectiveVersion);

                            detallesPlain = node.ToJsonString();
                        }
                        catch
                        {
                            // ignore merge failures and leave detallesPlain as-is
                        }
                    }
                }
                catch { }
                string? keyVerEnc = null;
                var encKey = _keyProvider?.GetEncryptionKey();
                if (encKey != null && encKey.Length > 0 && !string.IsNullOrEmpty(evento.Detalles))
                {
                    try
                    {
                        // Use AES-GCM if available (key must be 16/24/32 bytes). We'll generate a random nonce.
                        byte[] nonce = new byte[12];
                        RandomNumberGenerator.Fill(nonce);
                        byte[] plaintext = Encoding.UTF8.GetBytes(evento.Detalles ?? string.Empty);
                        byte[] cipher = new byte[plaintext.Length];
                        byte[] tag = new byte[16];
                        // Use constructor that specifies tag size to satisfy SYSLIB0053 guidance
                        using (var aesg = new AesGcm((ReadOnlySpan<byte>)encKey, 16))
                        {
                            aesg.Encrypt(nonce, plaintext, cipher, tag);
                        }

                        // store nonce|tag|cipher as base64
                        var combined = new byte[nonce.Length + tag.Length + cipher.Length];
                        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
                        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
                        Buffer.BlockCopy(cipher, 0, combined, nonce.Length + tag.Length, cipher.Length);
                        detallesEnc = Convert.ToBase64String(combined);
                        // Keep DetallesPlain for diagnostic/tests while storing encrypted blob in DetallesEnc.
                        // Note: in production deployments you may prefer to avoid storing plaintext alongside the encrypted blob.
                        keyVerEnc = _keyProvider!.GetEncryptionKeyVersion();
                    }
                    catch
                    {
                        // If encryption fails, fall back to plain text storage
                        detallesEnc = null;
                    }
                }

                // Decide which detalles value is used for payload/signature: prefer encrypted blob if created.
                var detallesForPayload = detallesEnc ?? detallesPlain ?? string.Empty;

                var payloadObj = new
                {
                    evento.EventId,
                    evento.UsuarioAdmin,
                    evento.Accion,
                    FechaHora = evento.FechaHora.ToString("o", CultureInfo.InvariantCulture),
                    evento.Modulo,
                    evento.UsuarioAfectado,
                    Resultado = resultadoStr,
                    Detalles = detallesForPayload
                };

                string payloadJson = JsonSerializer.Serialize(payloadObj);

                // We need to read the last hash and insert atomically to avoid race conditions.
                // Compute expensive items (encryption, signature, payload) before taking DB lock.
                string prevHash;
                string hash;
                try
                {
                    // Acquire an immediate transaction to prevent concurrent writers from
                    // observing the same PrevHash and breaking the chain.
                    using var beginCmd = conn.CreateCommand();
                    beginCmd.CommandText = "BEGIN IMMEDIATE;";
                    beginCmd.ExecuteNonQuery();

                    prevHash = ObtenerUltimoHash(conn) ?? string.Empty;

                    // Compute chained hash using the prevHash observed while holding the transaction
                    string hashInput = prevHash + "|" + payloadJson;
                    using (var sha = SHA256.Create())
                    {
                        hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(hashInput)));
                    }
                }
                catch
                {
                    // If we cannot start the transaction, fail safe by computing with empty prevHash
                    prevHash = string.Empty;
                    using (var sha = SHA256.Create())
                    {
                        hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes((prevHash ?? string.Empty) + "|" + payloadJson)));
                    }
                }

                // Compute HMAC signature if key available
                string signature = string.Empty;
                string? keyVer = null;
                var hmacKey = _keyProvider?.GetHmacKey();
                if (hmacKey != null && hmacKey.Length > 0)
                {
                    using var h = new HMACSHA256(hmacKey);
                    signature = Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(payloadJson)));
                    keyVer = _keyProvider!.GetHmacKeyVersion();
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO Auditoria (UsuarioAdmin, Accion, Fechahora, Modulo, UsuarioAfectado, Resultado, Detalles, DetallesPlain, DetallesEnc, Tipo, Rol, Area, SesionId, Equipo, VersionApp, EventId, PrevHash, Hash, Signature, KeyVersion, KeyVersionEnc)
                                    VALUES (@u, @a, @f, @m, @ua, @r, @d, @dp, @de, @t, @rol, @area, @ses, @eq, @ver, @eid, @prev, @hash, @sig, @kver, @kverenc);";

                // no console diagnostics during insert

                cmd.Parameters.AddWithValue("@u", evento.UsuarioAdmin ?? string.Empty);
                cmd.Parameters.AddWithValue("@a", evento.Accion ?? string.Empty);
                cmd.Parameters.AddWithValue("@f", evento.FechaHora.ToString("o", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@m", evento.Modulo ?? string.Empty);
                cmd.Parameters.AddWithValue("@ua", evento.UsuarioAfectado ?? string.Empty);
                cmd.Parameters.AddWithValue("@r", resultadoStr);
                // Persist plaintext details in DetallesPlain for diagnostics/tests while storing encrypted blob in DetallesEnc.
                // Legacy 'Detalles' will contain the value used for payload (encrypted blob when encryption enabled).
                var legacyDetalles = (object?)detallesForPayload ?? DBNull.Value;
                cmd.Parameters.AddWithValue("@d", legacyDetalles);
                // Persist DetallesPlain only when allowed. Avoid storing plaintext in Production unless explicitly enabled.
                var envName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
                var isProd = string.Equals(envName, "Production", StringComparison.OrdinalIgnoreCase);
                var allowPlain = !isProd || string.Equals(Environment.GetEnvironmentVariable("AUDIT_ALLOW_PLAINTEXT_DETAILS"), "1", StringComparison.OrdinalIgnoreCase);
                cmd.Parameters.AddWithValue("@dp", (object?)(allowPlain ? detallesPlain : null) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@de", (object?)detallesEnc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@t", evento.Tipo ?? string.Empty);
                cmd.Parameters.AddWithValue("@rol", effectiveRol ?? string.Empty);
                cmd.Parameters.AddWithValue("@area", effectiveArea ?? string.Empty);
                cmd.Parameters.AddWithValue("@ses", effectiveSesionId ?? string.Empty);
                cmd.Parameters.AddWithValue("@eq", effectiveEquipo ?? string.Empty);
                cmd.Parameters.AddWithValue("@ver", effectiveVersion ?? string.Empty);
                cmd.Parameters.AddWithValue("@eid", evento.EventId ?? Guid.NewGuid().ToString("N"));
                cmd.Parameters.AddWithValue("@prev", prevHash ?? string.Empty);
                cmd.Parameters.AddWithValue("@hash", hash ?? string.Empty);
                cmd.Parameters.AddWithValue("@sig", signature ?? string.Empty);
                cmd.Parameters.AddWithValue("@kver", keyVer ?? string.Empty);
                cmd.Parameters.AddWithValue("@kverenc", keyVerEnc ?? string.Empty);

                try
                {
                    cmd.ExecuteNonQuery();

                    // Commit explicit transaction if we started one
                    try
                    {
                        using var commitCmd = conn.CreateCommand();
                        commitCmd.CommandText = "COMMIT;";
                        commitCmd.ExecuteNonQuery();
                    }
                    catch { /* best-effort commit */ }
                }
                catch
                {
                    // Rollback if available
                    try
                    {
                        using var rb = conn.CreateCommand();
                        rb.CommandText = "ROLLBACK;";
                        rb.ExecuteNonQuery();
                    }
                    catch { }

                    throw;
                }

                // Fire-and-forget forwarding/exporting to avoid blocking UI callers
                var forwarderLocal = _forwarder;
                var eventIdLocal = evento.EventId ?? Guid.NewGuid().ToString("N");
                if (forwarderLocal != null)
                {
                    var signatureLocal = signature ?? string.Empty;
                    var forwardPayload = payloadJson;
                    Task.Run(async () =>
                    {
                        const int maxAttempts = 3;
                        int attempt = 0;
                        bool success = false;
                        while (attempt < maxAttempts)
                        {
                            attempt++;
                            try
                            {
                                await forwarderLocal.ForwardEventAsync(forwardPayload, signatureLocal).ConfigureAwait(false);
                                success = true;
                                break; // success
                            }
                            catch (Exception ex)
                            {
                                AuditLogHelper.Warning("AuditoriaService", $"Forward attempt {attempt} failed: {ex.Message}");
                                if (attempt >= maxAttempts)
                                {
                                    AuditLogHelper.Error("AuditoriaService", "Failed to forward audit event after retries", ex);
                                }
                                else
                                {
                                    try { await Task.Delay(200 * attempt).ConfigureAwait(false); } catch { }
                                }
                            }
                        }

                        if (!success)
                        {
                            try
                            {
                                var q = new AuditForwardQueue(_connectionString);
                                q.Enqueue(eventIdLocal, forwardPayload, signature ?? string.Empty);
                            }
                                catch (Exception ex)
                                {
                                    try { AuditLogHelper.Error("AuditoriaService", "Failed to enqueue forward after retries", ex); } catch { }
                                }
                        }
                    });
                }

                var exporterLocal = _exporter;
                if (exporterLocal != null)
                {
                    var signatureLocal = signature ?? string.Empty;
                    var payloadLocal = payloadJson;
                    Task.Run(async () =>
                    {
                        const int maxAttempts = 3;
                        int attempt = 0;
                        bool success = false;
                        while (attempt < maxAttempts)
                        {
                            attempt++;
                            try
                            {
                                await exporterLocal.ExportEventAsync(eventIdLocal, payloadLocal, signatureLocal).ConfigureAwait(false);
                                success = true;
                                break;
                            }
                            catch (Exception ex)
                            {
                                LogService.Warning("AuditoriaService", $"Export attempt {attempt} failed: {ex.Message}");
                                if (attempt >= maxAttempts)
                                {
                                    LogService.Error("AuditoriaService", "Failed to export audit event after retries", ex);
                                }
                                else
                                {
                                    try { await Task.Delay(250 * attempt).ConfigureAwait(false); } catch { }
                                }
                            }
                        }

                        if (!success)
                        {
                            try
                            {
                                var q = new AuditForwardQueue(_connectionString);
                                q.Enqueue(eventIdLocal, payloadLocal, signatureLocal);
                            }
                            catch (Exception ex)
                            {
                                try { LogService.Error("AuditoriaService", "Failed to enqueue export after retries", ex); } catch { }
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.Error("AuditoriaService", "RegistrarEvento failed", ex);
            }
        }

        private string? ObtenerUltimoHash(SqliteConnection conn)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Hash FROM Auditoria ORDER BY Id DESC LIMIT 1;";
                var val = cmd.ExecuteScalar();
                return val == null || val == DBNull.Value ? null : val.ToString();
            }
            catch
            {
                return null;
            }
        }

        public List<string> VerifyIntegrity()
        {
            var errors = new List<string>();
            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
                conn.Open();

                // Include legacy 'Detalles' column to detect tampering in older deployments where payload was stored there
                string q = "SELECT Id, PrevHash, Hash, Signature, KeyVersion, KeyVersionEnc, EventId, UsuarioAdmin, Accion, Fechahora, Modulo, UsuarioAfectado, Resultado, Detalles, DetallesPlain, DetallesEnc FROM Auditoria ORDER BY Id";
                using var cmd = conn.CreateCommand();
                cmd.CommandText = q;
                using var reader = cmd.ExecuteReader();

                string lastHash = string.Empty;
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string prev = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    string storedHash = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    string storedSig = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                    string keyVer = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                    string keyVerEnc = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);

                    // Recreate payload used for hash/signature
                    // Field indexes after SELECT: 0:Id,1:PrevHash,2:Hash,3:Signature,4:KeyVersion,5:KeyVersionEnc,6:EventId,7:UsuarioAdmin,8:Accion,9:Fechahora,10:Modulo,11:UsuarioAfectado,12:Resultado,13:Detalles(legacy),14:DetallesPlain,15:DetallesEnc
                    var detallesLegacyIdx = 13;
                    var detallesPlainIdx = 14;
                    var detallesEncIdx = 15;

                    var payload = new
                    {
                        EventId = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        UsuarioAdmin = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                        Accion = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                        FechaHora = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                        Modulo = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                        UsuarioAfectado = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                        Resultado = reader.IsDBNull(12) ? string.Empty : reader.GetValue(12).ToString(),
                        Detalles = reader.IsDBNull(detallesLegacyIdx)
                            ? (reader.IsDBNull(detallesPlainIdx)
                                ? (reader.IsDBNull(detallesEncIdx) ? string.Empty : reader.GetString(detallesEncIdx))
                                : reader.GetString(detallesPlainIdx))
                            : reader.GetString(detallesLegacyIdx)
                    };

                    string payloadJson = JsonSerializer.Serialize(payload);

                    // compute expected hash
                    string expectedHash;
                    using (var sha = SHA256.Create())
                    {
                        expectedHash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes((prev ?? string.Empty) + "|" + payloadJson)));
                    }

                    // Diagnostic output to help detect why integrity checks may pass/fail in tests
                    if (!string.Equals(expectedHash, storedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"Id={id}: hash mismatch (expected {expectedHash}, got {storedHash})");
                    }

                    // verify signature if key available
                    byte[]? key = _keyProvider?.GetHmacKeyByVersion(string.IsNullOrEmpty(keyVer) ? null : keyVer) ?? _keyProvider?.GetHmacKey();
                    if (key != null && key.Length > 0)
                    {
                        using var h = new HMACSHA256(key);
                        var computedSig = Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(payloadJson)));
                        if (!string.Equals(computedSig, storedSig, StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"Id={id}: signature mismatch");
                        }
                    }

                    lastHash = storedHash ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                errors.Add("Failed to verify integrity: " + ex.Message);
            }

            return errors;
        }
    }
}
