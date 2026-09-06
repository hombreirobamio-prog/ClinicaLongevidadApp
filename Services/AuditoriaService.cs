using ClinicaLongevidadApp.Models;
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
