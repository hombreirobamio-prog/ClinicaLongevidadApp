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
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

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

                // Verify what was stored immediately for diagnostics
                try
                {
                    using var checkCmd2 = conn.CreateCommand();
                    checkCmd2.CommandText = "SELECT Resultado FROM Auditoria ORDER BY Id DESC LIMIT 1";
                    var stored = checkCmd2.ExecuteScalar();
                    Console.WriteLine($"[AuditoriaService] After insert, stored Resultado={stored}");
                }
                catch { }

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
                        row[name] = val ?? (object)string.Empty;
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

                LogService.Info("AuditoriaService", $"Integrity diagnostic report written to {path}");
                return path;
            }
            catch (Exception ex)
            {
                LogService.Error("AuditoriaService", "Failed generating integrity diagnostic report.", ex);
                return string.Empty;
            }
        }

        private void IntentarAgregarColumna(SqliteConnection conn, string columnName, string type)
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
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                // Build canonical payload used for hashing/signing
                string resultadoStr = evento.Resultado ? "OK" : "ERROR";

                // Prepare plain/encrypted detalles. Encrypt first (if possible) so payload uses encrypted blob when available.
                string? detallesEnc = null;
                string? detallesPlain = evento.Detalles;
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
                        detallesPlain = null; // do not store plain if encrypted
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

                // Diagnostics: write stored values to console to help tests debug signature/hash issues.
                try
                {
                    Console.WriteLine($"[AuditoriaService] Inserting event. Resultado={resultadoStr}, DetallesPlainPresent={(detallesPlain != null)}, DetallesEncPresent={(detallesEnc != null)}");
                    Console.WriteLine($"[AuditoriaService] PayloadJson={payloadJson}");
                    Console.WriteLine($"[AuditoriaService] PrevHash={prevHash}, Hash={hash}, Signature={signature}");
                }
                catch { }

                cmd.Parameters.AddWithValue("@u", evento.UsuarioAdmin ?? string.Empty);
                cmd.Parameters.AddWithValue("@a", evento.Accion ?? string.Empty);
                cmd.Parameters.AddWithValue("@f", evento.FechaHora.ToString("o", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@m", evento.Modulo ?? string.Empty);
                cmd.Parameters.AddWithValue("@ua", evento.UsuarioAfectado ?? string.Empty);
                cmd.Parameters.AddWithValue("@r", resultadoStr);
                // Store plain detalles in legacy 'Detalles' column when available; keep DetallesPlain/DetallesEnc explicit
                // Store legacy 'Detalles' column with the value used for payload (prefer encrypted blob when present)
                cmd.Parameters.AddWithValue("@d", (object?)detallesForPayload ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@dp", (object?)detallesPlain ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@de", (object?)detallesEnc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@t", evento.Tipo ?? string.Empty);
                cmd.Parameters.AddWithValue("@rol", evento.Rol ?? string.Empty);
                cmd.Parameters.AddWithValue("@area", evento.Area ?? string.Empty);
                cmd.Parameters.AddWithValue("@ses", evento.SesionId ?? string.Empty);
                cmd.Parameters.AddWithValue("@eq", evento.Equipo ?? string.Empty);
                cmd.Parameters.AddWithValue("@ver", evento.VersionApp ?? string.Empty);
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
                if (forwarderLocal != null)
                {
                    var signatureLocal = signature ?? string.Empty;
                    var forwardPayload = payloadJson;
                    Task.Run(async () =>
                    {
                        const int maxAttempts = 3;
                        int attempt = 0;
                        while (attempt < maxAttempts)
                        {
                            attempt++;
                            try
                            {
                                await forwarderLocal.ForwardEventAsync(forwardPayload, signatureLocal).ConfigureAwait(false);
                                break; // success
                            }
                            catch (Exception ex)
                            {
                                LogService.Warning("AuditoriaService", $"Forward attempt {attempt} failed: {ex.Message}");
                                if (attempt >= maxAttempts)
                                {
                                    LogService.Error("AuditoriaService", "Failed to forward audit event after retries", ex);
                                }
                                else
                                {
                                    try { await Task.Delay(200 * attempt).ConfigureAwait(false); } catch { }
                                }
                            }
                        }
                    });
                }

                var exporterLocal = _exporter;
                if (exporterLocal != null)
                {
                    var eventIdLocal = evento.EventId ?? Guid.NewGuid().ToString("N");
                    var signatureLocal = signature ?? string.Empty;
                    var payloadLocal = payloadJson;
                    Task.Run(async () =>
                    {
                        const int maxAttempts = 3;
                        int attempt = 0;
                        while (attempt < maxAttempts)
                        {
                            attempt++;
                            try
                            {
                                await exporterLocal.ExportEventAsync(eventIdLocal, payloadLocal, signatureLocal).ConfigureAwait(false);
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
                using var conn = new SqliteConnection(_connectionString);
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
