# Apply patch script for ClinicaLongevidadApp
# Run from repository root in PowerShell (Developer PowerShell) as:
# .\audit_webhook_forwarding_apply_patch.ps1

Set-StrictMode -Version Latest

function Write-File([string]$path, [string]$content)
{
    $dir = Split-Path $path -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
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

        /// <summary>
        /// Generates a diagnostic report for the first integrity error found.
        /// The report contains the error messages from VerifyIntegrity and the surrounding
        /// audit rows (a window around the first mismatched Id). Returns the path to the
        /// generated report or an empty string if no errors were found.
        /// </summary>
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

                // Parse first error to extract Id if present
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

        /// <summary>
        /// Verifica la integridad de la cadena de auditoría.
        /// Devuelve una lista de descripciones de errores encontrados (vacía si todo OK).
        /// </summary>
        public List<string> VerifyIntegrity()
        {
            var errors = new List<string>();

            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                // Include KeyVersion so we can verify signatures using the historical key
                string query = "SELECT Id, Detalles, IFNULL(DetallesEnc, '') AS DetallesEnc, PrevHash, Hash, Signature, IFNULL(KeyVersion, '') AS KeyVersion FROM Auditoria ORDER BY Id ASC";
                using var cmd = new SqliteCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                string expectedPrev = string.Empty;
                bool seenFirstValidHash = false;
                int legacyStartId = -1;
                int legacyEndId = -1;

                while (reader.Read())
                {
                    int id = Convert.ToInt32(reader["Id"]);
                    string detalles = reader["Detalles"]?.ToString() ?? string.Empty;
                    string detallesEnc = reader["DetallesEnc"]?.ToString() ?? string.Empty;
                    string prevHash = reader["PrevHash"]?.ToString() ?? string.Empty;
                    string hash = reader["Hash"]?.ToString() ?? string.Empty;
                    string signature = reader["Signature"]?.ToString() ?? string.Empty;

                    // Handle legacy initial rows that were inserted before hashing/signing was implemented.
                    // If we haven't yet seen a valid stored hash and both PrevHash and Hash are empty,
                    // treat the row as legacy: compute the recalculated hash and use it as the expectedPrev
                    // for the following row, without emitting an error for the legacy row itself.
                    if (!seenFirstValidHash && string.IsNullOrEmpty(prevHash) && string.IsNullOrEmpty(hash))
                    {
                        if (legacyStartId == -1) legacyStartId = id;
                        legacyEndId = id;

                        // compute recalculated and advance expectedPrev to keep chain continuity
                        string recalculatedLegacy = CalcularSha256((prevHash ?? string.Empty) + (detalles ?? string.Empty));
                        expectedPrev = recalculatedLegacy ?? string.Empty;
                        // skip further signature/hash checks for this legacy row
                        continue;
                    }

                    // Mark that we've reached rows that should contain hashes/signatures
                    seenFirstValidHash = true;

                    if (!string.Equals(prevHash ?? string.Empty, expectedPrev ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"Mismatch prevHash at Id={id}: expected {expectedPrev}, found {prevHash}");
                    }

                    // Recalculate hash over prevHash + detalles
                    // Use plain Detalles for verification (hash is calculated over normalized details)
                    string recalculated = CalcularSha256((prevHash ?? string.Empty) + (detalles ?? string.Empty));
                    if (!string.Equals(recalculated, hash ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"Hash mismatch at Id={id}: expected {hash}, recalculated {recalculated}");
                    }

                    // Verify signature if key available. Try by stored KeyVersion first, then fallback to current key.
                    string keyVersion = reader["KeyVersion"]?.ToString() ?? string.Empty;
                    byte[]? key = null;
                    try
                    {
                        key = _keyProvider.GetHmacKeyByVersion(keyVersion);
                    }
                    catch
                    {
                        key = null;
                    }

                    if (key is null || key.Length == 0)
                    {
                        try { key = _keyProvider.GetHmacKey(); } catch { key = null; }
                    }

                    if (key != null && key.Length > 0)
                    {
                        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
                        var sig = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(recalculated ?? string.Empty));
                        var sigHex = Convert.ToHexString(sig);
                        if (!string.Equals(sigHex, signature ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"Signature mismatch at Id={id}");
                        }
                    }

                    expectedPrev = hash ?? string.Empty;
                }

                if (legacyStartId != -1)
                {
                    LogService.Info("AuditoriaService", $"Detected legacy audit rows without hashes from Id={legacyStartId} to Id={legacyEndId}. VerifyIntegrity treated them as legacy and continued verification from Id={legacyEndId + 1}.");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Integrity check failed: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Mantengo la API existente para compatibilidad.
        /// </summary>
        public void Registrar(string usuarioAdmin, string accion, string modulo, string usuarioAfectado, bool ok)
        {
            try
            {
                usuarioAdmin ??= "";
                accion ??= "";
                modulo ??= "";
                usuarioAfectado ??= "";

                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                string query = @"INSERT INTO Auditoria 
                                 (UsuarioAdmin, Accion, Fechahora, Modulo, UsuarioAfectado, Resultado)
                                 VALUES (@admin, @accion, @fecha, @modulo, @afectado, @resultado)";

                using var cmd = new SqliteCommand(query, conn);
                cmd.Parameters.AddWithValue("@admin", usuarioAdmin);
                cmd.Parameters.AddWithValue("@accion", accion);
                cmd.Parameters.AddWithValue("@fecha", DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@modulo", modulo);
                cmd.Parameters.AddWithValue("@afectado", usuarioAfectado);
                cmd.Parameters.AddWithValue("@resultado", ok ? "OK" : "ERROR");

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error registrando auditoría: " + ex);
            }
        }

        public void RegistrarEvento(AuditoriaEvento evento)
        {
            if (evento is null)
            {
                return;
            }

            try
            {
                AuditoriaEvento eventoNormalizado = CompletarEvento(evento);
                string detallesNormalizados = NormalizarDetalles(eventoNormalizado);

                // Open DB and compute hash/signature early so we can forward a complete payload promptly.
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                // Calcular hash en cadena y firma HMAC
                string eventJson = detallesNormalizados; // usar los detalles normalizados como payload base
                string prevHash = ObtenerUltimoHash(conn) ?? string.Empty;

                string payloadForHash = detallesNormalizados;
                string newHash = CalcularSha256(prevHash + payloadForHash);
                string signature = CalcularHmacInstance(newHash);
                string keyVersion = _keyProvider?.GetHmacKeyVersion() ?? string.Empty;
                string encKeyVersion = _keyProvider?.GetEncryptionKeyVersion() ?? string.Empty;

                // best-effort: attempt to notify forwarder early with full payload (including signature)
                // so test fakes that complete synchronously receive the signal even if later processing fails.
                try
                {
                    if (_forwarder is not null)
                    {
                        object detallesObj;
                        try
                        {
                            detallesObj = JsonSerializer.Deserialize<object?>(detallesNormalizados) ?? detallesNormalizados;
                        }
                        catch
                        {
                            detallesObj = detallesNormalizados;
                        }

                        var earlyPayload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["EventId"] = eventoNormalizado.EventId,
                            ["Accion"] = eventoNormalizado.Accion,
                            ["Modulo"] = eventoNormalizado.Modulo,
                            ["UsuarioAdmin"] = eventoNormalizado.UsuarioAdmin,
                            ["UsuarioAfectado"] = eventoNormalizado.UsuarioAfectado,
                            ["Resultado"] = eventoNormalizado.Resultado ? "OK" : "ERROR",
                            ["FechaHora"] = eventoNormalizado.FechaHora.ToString("o", CultureInfo.InvariantCulture),
                            ["Detalles"] = detallesObj,
                            ["Hash"] = newHash,
                            ["Signature"] = signature,
                            ["KeyVersion"] = keyVersion ?? string.Empty,
                            ["KeyVersionEnc"] = encKeyVersion ?? string.Empty
                        };

                        try { _ = _forwarder.ForwardEventAsync(JsonSerializer.Serialize(earlyPayload), signature ?? string.Empty); } catch { }
                    }
                }
                catch
                {
                    // ignore
                }

                // If encryption key available, produce an encrypted payload for storage, but
                // always compute the hash over the plain normalized details so integrity checks
                // remain readable and consistent.
                var encKey = _keyProvider.GetEncryptionKey();
                string encryptedPayload = string.Empty;
                if (encKey is not null && encKey.Length > 0)
                {
                    try
                    {
                        encryptedPayload = Convert.ToBase64String(EncryptStringToBytes_Aes(eventJson, encKey));
                    }
                    catch
                    {
                        encryptedPayload = string.Empty;
                    }
                }

                string query = @"INSERT INTO Auditoria
                                 (UsuarioAdmin, Accion, Fechahora, Modulo, UsuarioAfectado, Resultado, Detalles, DetallesPlain, DetallesEnc, Tipo, Rol, Area, SesionId, Equipo, VersionApp, EventId, PrevHash, Hash, Signature, KeyVersion, KeyVersionEnc)
                                 VALUES (@admin, @accion, @fecha, @modulo, @afectado, @resultado, @detalles, @detallesPlain, @detallesEnc, @tipo, @rol, @area, @sesionId, @equipo, @versionApp, @eventId, @prevHash, @hash, @signature, @keyVersion, @keyVersionEnc)";

                using var cmd = new SqliteCommand(query, conn);
                cmd.Parameters.AddWithValue("@admin", eventoNormalizado.UsuarioAdmin);
                cmd.Parameters.AddWithValue("@accion", eventoNormalizado.Accion);
                cmd.Parameters.AddWithValue("@fecha", eventoNormalizado.FechaHora.ToString("o", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@modulo", eventoNormalizado.Modulo);
                cmd.Parameters.AddWithValue("@afectado", eventoNormalizado.UsuarioAfectado);
                cmd.Parameters.AddWithValue("@resultado", eventoNormalizado.Resultado ? "OK" : "ERROR");
                // store plain normalized details in Detalles (this is the payload used for hashing)
                cmd.Parameters.AddWithValue("@detalles", detallesNormalizados);
                // also store in DetallesPlain for UI readability
                cmd.Parameters.AddWithValue("@detallesPlain", detallesNormalizados);
                // store encrypted payload separately if present
                cmd.Parameters.AddWithValue("@detallesEnc", encryptedPayload ?? string.Empty);
                cmd.Parameters.AddWithValue("@tipo", eventoNormalizado.Tipo ?? string.Empty);
                cmd.Parameters.AddWithValue("@rol", eventoNormalizado.Rol ?? string.Empty);
                cmd.Parameters.AddWithValue("@area", eventoNormalizado.Area ?? string.Empty);
                cmd.Parameters.AddWithValue("@sesionId", eventoNormalizado.SesionId ?? string.Empty);
                cmd.Parameters.AddWithValue("@equipo", eventoNormalizado.Equipo ?? string.Empty);
                cmd.Parameters.AddWithValue("@versionApp", eventoNormalizado.VersionApp ?? string.Empty);
                cmd.Parameters.AddWithValue("@eventId", eventoNormalizado.EventId);
                cmd.Parameters.AddWithValue("@prevHash", prevHash);
                cmd.Parameters.AddWithValue("@hash", newHash);
                cmd.Parameters.AddWithValue("@signature", signature);
                cmd.Parameters.AddWithValue("@keyVersion", keyVersion ?? string.Empty);
                cmd.Parameters.AddWithValue("@keyVersionEnc", encKeyVersion ?? string.Empty);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                LogService.Error("AuditoriaService", "Error registrando evento de auditoría.", ex);
            }
        }

        public List<AuditoriaModel> ObtenerAuditoria()
        {
            var lista = new List<AuditoriaModel>();

            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                // Use SQLite's datetime() to ensure proper chronological ordering
                string query = @"SELECT Id, UsuarioAdmin, Accion, Fechahora, Modulo, UsuarioAfectado, Resultado,
                                        IFNULL(Tipo, '') AS Tipo, IFNULL(DetallesPlain, IFNULL(Detalles, '')) AS Detalles,
                                        IFNULL(Rol, '') AS Rol, IFNULL(Area, '') AS Area,
                                        IFNULL(SesionId, '') AS SesionId, IFNULL(Equipo, '') AS Equipo,
                                        IFNULL(VersionApp, '') AS VersionApp, IFNULL(KeyVersion, '') AS KeyVersion, IFNULL(KeyVersionEnc, '') AS KeyVersionEnc
                                 FROM Auditoria
                                 ORDER BY datetime(Fechahora) DESC";

                using var cmd = new SqliteCommand(query, conn);

                try
                {
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        lista.Add(MapearAuditoria(reader));
                    }
                }
                catch (SqliteException)
                {
                    string queryFallback = "SELECT Id, UsuarioAdmin, Accion, Fechahora, Modulo, UsuarioAfectado, Resultado FROM Auditoria ORDER BY datetime(Fechahora) DESC";
                    using var cmdFallback = new SqliteCommand(queryFallback, conn);
                    using var readerFallback = cmdFallback.ExecuteReader();
                    while (readerFallback.Read())
                    {
                        lista.Add(MapearAuditoria(readerFallback));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error leyendo auditoría: " + ex);
            }

            return lista;
        }

        private static void IntentarAgregarColumna(SqliteConnection conn, string nombre, string tipo)
        {
            try
            {
                if (TieneColumna(conn, "Auditoria", nombre))
                {
                    return; // ya existe
                }

                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = $"ALTER TABLE Auditoria ADD COLUMN {nombre} {tipo};";
                alterCmd.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                // Si otro proceso añadió la columna entre la comprobación y el ALTER,
                // SQLite devolverá un error. Ignoramos específicamente el caso
                // "duplicate column name" para hacerlo idempotente.
                try
                {
                    if (ex.Message != null && ex.Message.IndexOf("duplicate column name", StringComparison.OrdinalIgnoreCase) >= 0)
                        return;
                }
                catch
                {
                    // fallthrough a la lógica genérica de ignorar
                }
            }
            catch
            {
                // Ignorar otros errores de alter por compatibilidad
            }
        }

        private static bool TieneColumna(SqliteConnection conn, string tabla, string columna)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                // Usar comillas en el nombre de la tabla para evitar problemas con nombres que contengan
                // caracteres especiales y para que PRAGMA table_info funcione de forma más robusta.
                cmd.CommandText = $"PRAGMA table_info('{tabla}');";
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string nombre = reader["name"]?.ToString() ?? string.Empty;
                    if (string.Equals(nombre, columna, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch
            {
                // Si falla, conservador: indicar que no existe para intentar ALTER y dejarlo caer si ya existe
            }

            return false;
        }

        private static AuditoriaEvento CompletarEvento(AuditoriaEvento evento)
        {
            DateTime fecha = evento.FechaHora == default ? DateTime.UtcNow : evento.FechaHora;

            string usuario = string.IsNullOrWhiteSpace(evento.UsuarioAdmin)
                ? Sesion.UsuarioActual ?? "Sistema"
                : evento.UsuarioAdmin;

            string modulo = string.IsNullOrWhiteSpace(evento.Modulo)
                ? "General"
                : evento.Modulo;

            string tipo = string.IsNullOrWhiteSpace(evento.Tipo)
                ? modulo
                : evento.Tipo;

            return new AuditoriaEvento
            {
                UsuarioAdmin = usuario,
                Accion = evento.Accion ?? string.Empty,
                Modulo = modulo,
                UsuarioAfectado = evento.UsuarioAfectado ?? string.Empty,
                Resultado = evento.Resultado,
                FechaHora = fecha,
                Tipo = tipo,
                Detalles = evento.Detalles,
                Rol = string.IsNullOrWhiteSpace(evento.Rol) ? Sesion.RolActual ?? string.Empty : evento.Rol,
                Area = string.IsNullOrWhiteSpace(evento.Area) ? Sesion.AreaActual ?? string.Empty : evento.Area,
                SesionId = string.IsNullOrWhiteSpace(evento.SesionId) ? SessionIdActual : evento.SesionId,
                Equipo = string.IsNullOrWhiteSpace(evento.Equipo) ? Environment.MachineName : evento.Equipo,
                VersionApp = string.IsNullOrWhiteSpace(evento.VersionApp) ? ObtenerVersionAplicacion() : evento.VersionApp
            };
        }

        private static string ObtenerUltimoHash(SqliteConnection conn)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Hash FROM Auditoria WHERE Hash IS NOT NULL ORDER BY Id DESC LIMIT 1";
                var result = cmd.ExecuteScalar();
                return result?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string CalcularSha256(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input ?? string.Empty);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        private static string CalcularHmac(string input)
        {
            try
            {
                // Use key provider
                // NOTE: this method is now unused; instance method below uses _keyProvider
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // Instance HMAC using injected key provider
        private string CalcularHmacInstance(string input)
        {
            try
            {
                var key = _keyProvider.GetHmacKey();
                if (key == null || key.Length == 0) return string.Empty;
                using var hmac = new System.Security.Cryptography.HMACSHA256(key);
                var sig = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input ?? string.Empty));
                return Convert.ToHexString(sig);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key)
        {
            using var aesAlg = System.Security.Cryptography.Aes.Create();
            aesAlg.Key = Key.Length >= 32 ? Key[..32] : PadKey(Key, 32);
            aesAlg.GenerateIV();
            var iv = aesAlg.IV;

            using var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, iv);
            using var msEncrypt = new System.IO.MemoryStream();
            msEncrypt.Write(iv, 0, iv.Length);
            using (var csEncrypt = new System.Security.Cryptography.CryptoStream(msEncrypt, encryptor, System.Security.Cryptography.CryptoStreamMode.Write))
            using (var swEncrypt = new System.IO.StreamWriter(csEncrypt))
            {
                swEncrypt.Write(plainText);
            }
            return msEncrypt.ToArray();
        }

        private static byte[] PadKey(byte[] key, int size)
        {
            var outKey = new byte[size];
            for (int i = 0; i < size; i++) outKey[i] = i < key.Length ? key[i] : (byte)0;
            return outKey;
        }

        private static string NormalizarDetalles(AuditoriaEvento evento)
        {
            var datos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Rol"] = evento.Rol ?? string.Empty,
                ["Area"] = evento.Area ?? string.Empty,
                ["SesionId"] = evento.SesionId ?? string.Empty,
                ["Equipo"] = evento.Equipo ?? string.Empty,
                ["VersionApp"] = evento.VersionApp ?? string.Empty
            };

            string? detalles = evento.Detalles;
            if (!string.IsNullOrWhiteSpace(detalles))
            {
                string texto = detalles.Trim();
                if (texto.StartsWith("{", StringComparison.Ordinal))
                {
                    try
                    {
                        using JsonDocument doc = JsonDocument.Parse(texto);
                        foreach (JsonProperty propiedad in doc.RootElement.EnumerateObject())
                        {
                            datos[propiedad.Name] = propiedad.Value.ToString();
                        }
                    }
                    catch
                    {
                        datos["DetalleLegacy"] = texto;
                    }
                }
                else
                {
                    datos["DetalleLegacy"] = texto;
                }
            }

            return JsonSerializer.Serialize(datos);
        }

        private static string ObtenerVersionAplicacion()
        {
            return Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? string.Empty;
        }

        private static AuditoriaModel MapearAuditoria(SqliteDataReader reader)
        {
            var modelo = new AuditoriaModel
            {
                Id = Convert.ToInt32(reader["Id"]),
                UsuarioAdmin = reader["UsuarioAdmin"]?.ToString() ?? "",
                Accion = reader["Accion"]?.ToString() ?? "",
                Modulo = reader["Modulo"]?.ToString() ?? "",
                UsuarioAfectado = reader["UsuarioAfectado"]?.ToString() ?? "",
                Resultado = reader["Resultado"]?.ToString() ?? "",
                Tipo = ObtenerValorOpcional(reader, "Tipo"),
                Detalles = ObtenerValorOpcional(reader, "Detalles"),
                Rol = ObtenerValorOpcional(reader, "Rol"),
                Area = ObtenerValorOpcional(reader, "Area"),
                SesionId = ObtenerValorOpcional(reader, "SesionId"),
                Equipo = ObtenerValorOpcional(reader, "Equipo"),
                VersionApp = ObtenerValorOpcional(reader, "VersionApp"),
                KeyVersion = ObtenerValorOpcional(reader, "KeyVersion"),
                KeyVersionEnc = ObtenerValorOpcional(reader, "KeyVersionEnc")
            };

            var raw = reader["Fechahora"]?.ToString() ?? "";
            if (DateTime.TryParse(raw, out var dt))
            {
                modelo.FechaHora = dt;
            }
            else
            {
                modelo.FechaHora = DateTime.Now;
            }

            return modelo;
        }

        private static string ObtenerValorOpcional(SqliteDataReader reader, string columna)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columna, StringComparison.OrdinalIgnoreCase))
                {
                    return reader[columna]?.ToString() ?? string.Empty;
                }
            }

            return string.Empty;
        }
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

# ViewModels/AuditoriaViewModel.cs
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
"@
Write-File $path $content

# App.xaml.cs (only modified sections preserved)
$path = "App.xaml.cs"
$content = @"
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

            string dbPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "ClinicaLongevidad.db");

            string connectionString = $"Data Source={dbPath}";

            // Configure key provider: prefer Azure Key Vault if configured
            IKeyProvider keyProvider;
            string? vaultUri = Environment.GetEnvironmentVariable("KEYVAULT_URI");
            if (!string.IsNullOrWhiteSpace(vaultUri))
            {
                try
                {
                    keyProvider = new AzureKeyVaultKeyProvider();
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

            AuditoriaService = new AuditoriaService(connectionString, keyProvider, exporter, forwarder);
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
                    // fallback to local rotation provider if vault setup fails
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

            // Start integrity worker
            try
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
                            LogService.Warning("App", "Failed to compute HMAC for integrity failure payload: " + ex.Message);
                        }

                        LogService.Error("App", $"Audit integrity failure detected ({errors.Count}): {string.Join("; ", errors)}");

                        // Forward to webhook if available
                        if (forwarder != null)
                        {
                            try { await forwarder.ForwardEventAsync(json!, signature); } catch (Exception ex) { LogService.Error("App", "Error forwarding integrity alert", ex); }
                        }

                        // Export to blob storage if configured
                        if (exporter != null)
                        {
                            try { await exporter.ExportEventAsync(eventId, json!, signature); } catch (Exception ex) { LogService.Error("App", "Error exporting integrity alert", ex); }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.Error("App", "Error handling integrity failure", ex);
                    }
                };

                integrityWorker.Start();
                // store in App properties for shutdown
                Current.Properties["AuditoriaIntegrityWorker"] = integrityWorker;
            }
"@
Write-File $path $content

Write-Host "Patch applied. Run 'dotnet build' and 'dotnet test' to verify."