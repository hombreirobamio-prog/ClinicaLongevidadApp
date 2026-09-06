using System;
using System;
using System.IO;
using System.Security.Cryptography;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.Models;

namespace RotateKeysTool
{
    internal class Program
    {
        private static bool AssumeYes = false;
        static int Main(string[] args)
        {
            // Global flags
            for (int ai = 0; ai < args.Length; ai++)
            {
                if (string.Equals(args[ai], "--yes", StringComparison.OrdinalIgnoreCase) || string.Equals(args[ai], "-y", StringComparison.OrdinalIgnoreCase))
                {
                    AssumeYes = true;
                    break;
                }
            }

            // Top-level command handling: export-delta, restore, replay-delta
            if (args.Length > 0)
            {
                var cmd = args[0];
                if (string.Equals(cmd, "export-delta", StringComparison.OrdinalIgnoreCase))
                {
                    return HandleExportDelta(args);
                }
                if (string.Equals(cmd, "restore", StringComparison.OrdinalIgnoreCase))
                {
                    return HandleRestore(args);
                }
                if (string.Equals(cmd, "replay-delta", StringComparison.OrdinalIgnoreCase))
                {
                    return HandleReplayDelta(args);
                }
            }
            bool rotateHmac = false;
            bool rotateEnc = false;
            bool dryRun = false;
            bool verifyAfter = false;
            bool backfill = false;
            bool apply = false;
            bool force = false;
            string providerChoice = string.Empty;
            string? connectionString = null;

            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                switch (a)
                {
                    case "--hmac":
                        rotateHmac = true; break;
                    case "--enc":
                        rotateEnc = true; break;
                    case "--all":
                        rotateHmac = rotateEnc = true; break;
                    case "--dry-run":
                        dryRun = true; break;
                    case "--verify-after":
                        verifyAfter = true; break;
                    case "--backfill":
                        backfill = true; break;
                    case "--apply":
                        apply = true; break;
                    case "--force":
                        force = true; break;
                    case "--db":
                        if (i + 1 < args.Length) connectionString = args[++i];
                        break;
                    case "--provider":
                        if (i + 1 < args.Length) providerChoice = args[++i];
                        break;
                    case "-h":
                    case "--help":
                        PrintHelp(); return 0;
                }
            }

            if (!rotateHmac && !rotateEnc)
            {
                Console.WriteLine("Nothing to do. Use --hmac, --enc or --all. Use --help for usage.");
                return 1;
            }

            if (backfill && verifyAfter)
            {
                Console.WriteLine("Note: --backfill and --verify-after can be combined; verify will run after backfill.");
            }

            IKeyRotationProvider rotationProvider = CreateRotationProvider(providerChoice);
            if (rotationProvider == null)
            {
                Console.WriteLine("No rotation provider available.");
                return 2;
            }

            if (dryRun)
            {
                if (rotateHmac)
                {
                    var hmac = GenerateRandomKey(32);
                    Console.WriteLine("HMAC (base64): " + Convert.ToBase64String(hmac));
                }
                if (rotateEnc)
                {
                    var enc = GenerateRandomKey(32);
                    Console.WriteLine("ENC  (base64): " + Convert.ToBase64String(enc));
                }
                return 0;
            }

            try
            {
                if (rotateHmac)
                {
                    var hmac = GenerateRandomKey(32);
                    rotationProvider.PersistHmacKey(hmac);
                    Console.WriteLine("HMAC key rotated (persisted).");
                }

                if (rotateEnc)
                {
                    var enc = GenerateRandomKey(32);
                    rotationProvider.PersistEncryptionKey(enc);
                    Console.WriteLine("Encryption key rotated (persisted).");
                }

                Console.WriteLine("Rotation complete.");

                if (verifyAfter)
                {
                    var conn = connectionString ?? Environment.GetEnvironmentVariable("AUDIT_DB") ?? "Data Source=auditoria.db";
                    Console.WriteLine($"Running VerifyIntegrity on '{conn}'...");
                    var svc = new AuditoriaService(conn);
                    var errors = svc.VerifyIntegrity();
                    if (errors != null && errors.Count > 0)
                    {
                        Console.WriteLine("VerifyIntegrity returned errors:");
                        foreach (var e in errors) Console.WriteLine(" - " + e);
                        return 4;
                    }
                    Console.WriteLine("VerifyIntegrity: OK (no errors)");
                }

                // Backfill processing (runs after rotation if requested)
                if (backfill)
                {
                    var connStr = connectionString ?? Environment.GetEnvironmentVariable("AUDIT_DB") ?? "Data Source=auditoria.db";
                    Console.WriteLine($"Running backfill on '{connStr}' (apply={apply}, force={force})...");
                    var keyProvider = CreateKeyProvider(providerChoice);
                    var result = BackfillIntegrity(connStr, keyProvider, apply, force);
                    if (!result)
                    {
                        Console.WriteLine("Backfill encountered errors.");
                        return 5;
                    }
                    Console.WriteLine("Backfill completed.");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Operation failed: " + ex.Message);
                return 3;
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("RotateKeys tool\n");
            Console.WriteLine("Usage: RotateKeys [--hmac] [--enc] [--all] [--provider azure|local] [--dry-run]");
            Console.WriteLine("If --provider is not specified, the tool prefers Azure Key Vault when KEYVAULT_URI is set.");
        }

        static IKeyRotationProvider CreateRotationProvider(string choice)
        {
            if (!string.IsNullOrWhiteSpace(choice))
            {
                if (string.Equals(choice, "azure", StringComparison.OrdinalIgnoreCase))
                {
                    try { return new AzureKeyRotationProvider(); } catch { return null; }
                }
                if (string.Equals(choice, "local", StringComparison.OrdinalIgnoreCase))
                {
                    return new LocalKeyRotationProvider();
                }
            }

            // Auto-detect
            var kv = Environment.GetEnvironmentVariable("KEYVAULT_URI");
            if (!string.IsNullOrWhiteSpace(kv))
            {
                try { return new AzureKeyRotationProvider(); } catch { }
            }

            return new LocalKeyRotationProvider();
        }

        static byte[] GenerateRandomKey(int bytes)
        {
            var key = new byte[bytes];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(key);
            return key;
        }

        // -------------------- Export / Restore / Replay implementations --------------------
        static int HandleExportDelta(string[] args)
        {
            string? backupFile = null;
            string outFile = "delta.json";
            string? currentDb = null;

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--backup": if (i + 1 < args.Length) backupFile = args[++i]; break;
                    case "--out": if (i + 1 < args.Length) outFile = args[++i]; break;
                    case "--db": if (i + 1 < args.Length) currentDb = args[++i]; break;
                }
            }

            if (string.IsNullOrWhiteSpace(backupFile))
            {
                Console.WriteLine("Missing --backup <backupFile>");
                return 2;
            }

            var currentConn = currentDb ?? Environment.GetEnvironmentVariable("AUDIT_DB") ?? "Data Source=auditoria.db";

            try
            {
                long maxIdInBackup = 0;
                using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={backupFile}"))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT IFNULL(MAX(Id),0) FROM Auditoria";
                    var res = cmd.ExecuteScalar();
                    maxIdInBackup = Convert.ToInt64(res);
                }

                var delta = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
                using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(currentConn))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, UsuarioAdmin, Accion, Fechahora, Modulo, UsuarioAfectado, Resultado, Detalles, DetallesEnc, Tipo, Rol, Area, SesionId, Equipo, VersionApp FROM Auditoria WHERE Id > @max ORDER BY Id";
                    cmd.Parameters.AddWithValue("@max", maxIdInBackup);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var row = new System.Collections.Generic.Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string name = reader.GetName(i);
                            object val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            row[name] = val ?? string.Empty;
                        }
                        delta.Add(row);
                    }
                }

                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(outFile, System.Text.Json.JsonSerializer.Serialize(delta, options));
                Console.WriteLine($"Exported {delta.Count} rows to {outFile}");
                // best-effort audit log
                ToolAuditLogger.RegistrarOperacion(currentConn, "export-delta", true, new { backup = backupFile, outfile = outFile, rows = delta.Count, sourceDb = currentConn });

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("export-delta failed: " + ex.Message);
                // best-effort audit log for failure
                ToolAuditLogger.RegistrarOperacion(currentConn, "export-delta", false, ex.Message);
                return 3;
            }
        }

        static int HandleRestore(string[] args)
        {
            string? backupFile = null;
            string? dbConn = null;
            bool backupCurrent = true;
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--backup": if (i + 1 < args.Length) backupFile = args[++i]; break;
                    case "--db": if (i + 1 < args.Length) dbConn = args[++i]; break;
                    case "--no-backup-current": backupCurrent = false; break;
                }
            }

            if (string.IsNullOrWhiteSpace(backupFile))
            {
                Console.WriteLine("Missing --backup <backupFile>");
                return 2;
            }

            var conn = dbConn ?? Environment.GetEnvironmentVariable("AUDIT_DB") ?? "Data Source=auditoria.db";
            var dbFile = GetSqliteFilePathFromConnectionString(conn) ?? string.Empty;
            if (string.IsNullOrEmpty(dbFile))
            {
                Console.WriteLine("Target DB is not file-based or could not be determined.");
                return 4;
            }

            try
            {
                if (backupCurrent)
                {
                    var backupPath = CreateDatabaseBackupPath(dbFile);
                    File.Copy(dbFile, backupPath);
                    Console.WriteLine($"Current DB backed up to {backupPath}");
                }

                // Confirm destructive action
                if (!AssumeYes)
                {
                    Console.WriteLine($"About to replace target DB file '{dbFile}' with backup '{backupFile}'. This is destructive. Continue? (yes/no)");
                    var resp = Console.ReadLine();
                    if (resp is null || !(resp.Equals("yes", StringComparison.OrdinalIgnoreCase) || resp.Equals("y", StringComparison.OrdinalIgnoreCase)))
                    {
                        Console.WriteLine("Restore aborted by user.");
                        // best-effort audit: user aborted restore
                        ToolAuditLogger.RegistrarOperacion(conn, "restore", false, $"Aborted by user while attempting to restore from {backupFile} to {dbFile}");
                        return 6;
                    }
                }

                // Replace target DB file with provided backup file
                File.Copy(backupFile, dbFile, overwrite: true);
                Console.WriteLine("Restore completed.");

                // best-effort audit log for successful restore
                ToolAuditLogger.RegistrarOperacion(conn, "restore", true, new { backup = backupFile, target = dbFile });

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("restore failed: " + ex.Message);
                // best-effort audit log for failed restore
                ToolAuditLogger.RegistrarOperacion(conn, "restore", false, ex.Message);
                return 5;
            }
        }

        static int HandleReplayDelta(string[] args)
        {
            string? inFile = null;
            string? dbConn = null;
            string? providerChoice = null;
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--in": if (i + 1 < args.Length) inFile = args[++i]; break;
                    case "--db": if (i + 1 < args.Length) dbConn = args[++i]; break;
                    case "--provider": if (i + 1 < args.Length) providerChoice = args[++i]; break;
                }
            }

            if (string.IsNullOrWhiteSpace(inFile))
            {
                Console.WriteLine("Missing --in <delta.json>");
                return 2;
            }

            var connStr = dbConn ?? Environment.GetEnvironmentVariable("AUDIT_DB") ?? "Data Source=auditoria.db";
            var keyProvider = CreateKeyProvider(providerChoice ?? string.Empty);

            try
            {
                var json = File.ReadAllText(inFile);
                var docs = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>>(json)!;

                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connStr);
                conn.Open();

                // determine last hash
                string lastHash = string.Empty;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Hash FROM Auditoria WHERE Hash IS NOT NULL ORDER BY Id DESC LIMIT 1";
                    var res = cmd.ExecuteScalar();
                    lastHash = res?.ToString() ?? string.Empty;
                }

                // Confirm before applying potentially large number of inserts
                if (!AssumeYes)
                {
                    Console.WriteLine($"About to replay {docs.Count} events into DB '{connStr}'. This will insert new rows. Continue? (yes/no)");
                    var r = Console.ReadLine();
                    if (r is null || !(r.Equals("yes", StringComparison.OrdinalIgnoreCase) || r.Equals("y", StringComparison.OrdinalIgnoreCase)))
                    {
                        Console.WriteLine("Replay aborted by user.");
                        // best-effort audit: user aborted replay-delta
                        ToolAuditLogger.RegistrarOperacion(connStr, "replay-delta", false, $"Aborted by user while attempting to replay {docs.Count} events from {inFile}");

                        return 7;
                    }
                }

                using var tx = conn.BeginTransaction();
                foreach (var item in docs)
                {
                    // assume Detalles contains normalized payload
                    string detalles = item.ContainsKey("Detalles") ? (item["Detalles"]?.ToString() ?? string.Empty) : string.Empty;
                    string prev = lastHash ?? string.Empty;
                    string newHash = CalcularSha256(prev + detalles);

                    // compute signature
                    byte[]? key = null;
                    try { key = keyProvider.GetHmacKey(); } catch { key = null; }
                    string signature = string.Empty;
                    string keyVersion = keyProvider.GetHmacKeyVersion() ?? string.Empty;
                    if (key != null && key.Length > 0)
                    {
                        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
                        var sig = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(newHash ?? string.Empty));
                        signature = Convert.ToHexString(sig);
                    }

                    // Insert row using fields from item
                    using var ins = conn.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = @"INSERT INTO Auditoria (UsuarioAdmin, Accion, Fechahora, Modulo, UsuarioAfectado, Resultado, Detalles, DetallesPlain, DetallesEnc, Tipo, Rol, Area, SesionId, Equipo, VersionApp, EventId, PrevHash, Hash, Signature, KeyVersion, KeyVersionEnc)
VALUES (@admin,@accion,@fecha,@modulo,@afectado,@resultado,@detalles,@detallesPlain,@detallesEnc,@tipo,@rol,@area,@sesionId,@equipo,@versionApp,@eventId,@prevHash,@hash,@signature,@keyVersion,@keyVersionEnc)";

                    ins.Parameters.AddWithValue("@admin", item.ContainsKey("UsuarioAdmin") ? item["UsuarioAdmin"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@accion", item.ContainsKey("Accion") ? item["Accion"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@fecha", item.ContainsKey("Fechahora") ? item["Fechahora"]?.ToString() ?? DateTime.Now.ToString("o") : DateTime.Now.ToString("o"));
                    ins.Parameters.AddWithValue("@modulo", item.ContainsKey("Modulo") ? item["Modulo"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@afectado", item.ContainsKey("UsuarioAfectado") ? item["UsuarioAfectado"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@resultado", item.ContainsKey("Resultado") ? item["Resultado"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@detalles", detalles);
                    ins.Parameters.AddWithValue("@detallesPlain", detalles);
                    ins.Parameters.AddWithValue("@detallesEnc", item.ContainsKey("DetallesEnc") ? item["DetallesEnc"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@tipo", item.ContainsKey("Tipo") ? item["Tipo"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@rol", item.ContainsKey("Rol") ? item["Rol"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@area", item.ContainsKey("Area") ? item["Area"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@sesionId", item.ContainsKey("SesionId") ? item["SesionId"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@equipo", item.ContainsKey("Equipo") ? item["Equipo"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@versionApp", item.ContainsKey("VersionApp") ? item["VersionApp"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@eventId", item.ContainsKey("EventId") ? item["EventId"]?.ToString() ?? string.Empty : string.Empty);
                    ins.Parameters.AddWithValue("@prevHash", prev);
                    ins.Parameters.AddWithValue("@hash", newHash);
                    ins.Parameters.AddWithValue("@signature", signature);
                    ins.Parameters.AddWithValue("@keyVersion", keyVersion ?? string.Empty);
                    ins.Parameters.AddWithValue("@keyVersionEnc", item.ContainsKey("KeyVersionEnc") ? item["KeyVersionEnc"]?.ToString() ?? string.Empty : string.Empty);

                    ins.ExecuteNonQuery();

                    lastHash = newHash;
                }

                tx.Commit();
                Console.WriteLine($"Replayed {docs.Count} events into DB.");
                // best-effort audit log for successful replay-delta
                ToolAuditLogger.RegistrarOperacion(connStr, "replay-delta", true, new { infile = inFile, count = docs.Count, db = connStr });
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("replay-delta failed: " + ex.Message);
                // best-effort audit log for failed replay-delta
                ToolAuditLogger.RegistrarOperacion(connStr, "replay-delta", false, ex.Message);
                return 6;
            }
        }

        static IKeyProvider CreateKeyProvider(string choice)
        {
            if (!string.IsNullOrWhiteSpace(choice))
            {
                if (string.Equals(choice, "azure", StringComparison.OrdinalIgnoreCase))
                {
                    try { return new AzureKeyVaultKeyProvider(); } catch { }
                }
                if (string.Equals(choice, "local", StringComparison.OrdinalIgnoreCase))
                {
                    return new LocalKeyProvider();
                }
            }

            var kv = Environment.GetEnvironmentVariable("KEYVAULT_URI");
            if (!string.IsNullOrWhiteSpace(kv))
            {
                try { return new AzureKeyVaultKeyProvider(); } catch { }
            }

            return new LocalKeyProvider();
        }

        static bool BackfillIntegrity(string connectionString, IKeyProvider keyProvider, bool apply, bool force)
        {
            try
            {
                // If applying changes, create a filesystem backup of the SQLite DB first.
                if (apply)
                {
                    try
                    {
                        var dbFile = GetSqliteFilePathFromConnectionString(connectionString);
                        if (!string.IsNullOrEmpty(dbFile) && File.Exists(dbFile))
                        {
                            var backupPath = CreateDatabaseBackupPath(dbFile);
                            File.Copy(dbFile, backupPath, overwrite: false);
                            Console.WriteLine($"Database backup created: {backupPath}");
                        }
                        else
                        {
                            Console.WriteLine("No file-based SQLite DB detected or file not found; skipping automatic backup.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: failed to create DB backup: " + ex.Message);
                        Console.WriteLine("Aborting operation because backup is required when --apply is used.");
                        return false;
                    }
                }
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
                conn.Open();

                string query = "SELECT Id, Detalles, IFNULL(DetallesEnc, '') AS DetallesEnc, PrevHash, Hash, Signature, IFNULL(KeyVersion, '') AS KeyVersion FROM Auditoria ORDER BY Id ASC";
                using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                string expectedPrev = string.Empty;
                bool seenFirstValidHash = false;

                var updates = new System.Collections.Generic.List<(int Id, string Prev, string Hash, string Signature, string KeyVersion)>();

                while (reader.Read())
                {
                    int id = Convert.ToInt32(reader["Id"]);
                    string detalles = reader["Detalles"]?.ToString() ?? string.Empty;
                    string prevHash = reader["PrevHash"]?.ToString() ?? string.Empty;
                    string hash = reader["Hash"]?.ToString() ?? string.Empty;
                    string signature = reader["Signature"]?.ToString() ?? string.Empty;
                    string keyVersion = reader["KeyVersion"]?.ToString() ?? string.Empty;

                    if (!seenFirstValidHash && string.IsNullOrEmpty(prevHash) && string.IsNullOrEmpty(hash))
                    {
                        // legacy row - compute recalculated and advance expectedPrev
                        string recalculatedLegacy = CalcularSha256((prevHash ?? string.Empty) + (detalles ?? string.Empty));
                        expectedPrev = recalculatedLegacy ?? string.Empty;
                        continue;
                    }

                    seenFirstValidHash = true;

                    // if prevHash mismatch, plan to set it to expectedPrev when empty or if force
                    string newPrev = prevHash;
                    if (!string.Equals(prevHash ?? string.Empty, expectedPrev ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(prevHash) || force)
                        {
                            newPrev = expectedPrev ?? string.Empty;
                        }
                        else
                        {
                            Console.WriteLine($"Id={id}: prevHash mismatch (expected {expectedPrev}, found {prevHash})");
                        }
                    }

                    // recalculated hash over prevHash + detalles (use the newPrev for calculation)
                    string recalculated = CalcularSha256((newPrev ?? string.Empty) + (detalles ?? string.Empty));
                    string newHash = hash;
                    if (!string.Equals(recalculated, hash ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(hash) || force)
                        {
                            newHash = recalculated ?? string.Empty;
                        }
                        else
                        {
                            Console.WriteLine($"Id={id}: hash mismatch (expected {hash}, recalculated {recalculated})");
                        }
                    }

                    // compute signature using key provider (try by stored keyVersion then fallback)
                    byte[]? key = null;
                    try { key = keyProvider.GetHmacKeyByVersion(keyVersion); } catch { key = null; }
                    if (key is null || key.Length == 0)
                    {
                        try { key = keyProvider.GetHmacKey(); } catch { key = null; }
                    }

                    string newSignature = signature;
                    string newKeyVersion = keyVersion;
                    if (key != null && key.Length > 0)
                    {
                        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
                        var sig = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(recalculated ?? string.Empty));
                        var sigHex = Convert.ToHexString(sig);
                        if (!string.Equals(sigHex, signature ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrEmpty(signature) || force)
                            {
                                newSignature = sigHex;
                                try { newKeyVersion = keyProvider.GetHmacKeyVersion() ?? string.Empty; } catch { }
                            }
                            else
                            {
                                Console.WriteLine($"Id={id}: signature mismatch");
                            }
                        }
                    }

                    updates.Add((id, newPrev ?? string.Empty, newHash ?? string.Empty, newSignature ?? string.Empty, newKeyVersion ?? string.Empty));

                    expectedPrev = newHash ?? string.Empty;
                }

                reader.Close();

                if (updates.Count == 0)
                {
                    Console.WriteLine("No updates required.");
                    return true;
                }

                Console.WriteLine($"Planned updates for {updates.Count} rows. apply={apply}");
                foreach (var u in updates)
                {
                    Console.WriteLine($"Id={u.Id} PrevHash={u.Prev} Hash={u.Hash} Signature={(string.IsNullOrEmpty(u.Signature) ? "(empty)" : "(set)")} KeyVersion={u.KeyVersion}");
                }

                if (!apply)
                {
                    Console.WriteLine("Dry-run mode: no changes applied. Rerun with --apply to persist updates.");
                    return true;
                }

                using var tx = conn.BeginTransaction();
                foreach (var u in updates)
                {
                    using var upCmd = conn.CreateCommand();
                    upCmd.CommandText = "UPDATE Auditoria SET PrevHash = @prev, Hash = @hash, Signature = @signature, KeyVersion = @kv WHERE Id = @id";
                    upCmd.Parameters.AddWithValue("@prev", u.Prev);
                    upCmd.Parameters.AddWithValue("@hash", u.Hash);
                    upCmd.Parameters.AddWithValue("@signature", u.Signature);
                    upCmd.Parameters.AddWithValue("@kv", u.KeyVersion ?? string.Empty);
                    upCmd.Parameters.AddWithValue("@id", u.Id);
                    upCmd.Transaction = tx;
                    upCmd.ExecuteNonQuery();
                }
                tx.Commit();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Backfill failed: " + ex.Message);
                return false;
            }
        }

        static string CalcularSha256(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input ?? string.Empty);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        static string? GetSqliteFilePathFromConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return null;
            // Look for Data Source=...
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                if (p.StartsWith("Data Source", StringComparison.OrdinalIgnoreCase) || p.StartsWith("DataSource", StringComparison.OrdinalIgnoreCase))
                {
                    var eq = p.IndexOf('=');
                    if (eq >= 0 && eq + 1 < p.Length)
                    {
                        var val = p[(eq + 1)..].Trim();
                        // Remove surrounding quotes
                        if ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'")))
                            val = val[1..^1];
                        // Ignore in-memory DB
                        if (string.Equals(val, ":memory:", StringComparison.OrdinalIgnoreCase)) return null;
                        return val;
                    }
                }
            }
            return null;
        }

        static string CreateDatabaseBackupPath(string dbFile)
        {
            var dir = Path.GetDirectoryName(dbFile) ?? Directory.GetCurrentDirectory();
            var name = Path.GetFileNameWithoutExtension(dbFile);
            var ext = Path.GetExtension(dbFile);
            var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupName = $"{name}_backup_{ts}{ext}";
            var backupPath = Path.Combine(dir, backupName);
            return backupPath;
        }
    }
}
