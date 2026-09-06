using System;
using System;
using System.IO;
using Microsoft.Data.Sqlite;
using System.Reflection;
using System.Security.Cryptography;

class Program
{
    static int Main(string[] args)
    {
        try
        {
            string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicaLongevidad.db");
            string connectionString = $"Data Source={dbPath}";

            // Directly inspect the SQLite DB and generate a diagnostic report without loading the main assembly.
            string dbFullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicaLongevidad.db");
            if (!File.Exists(dbFullPath))
            {
                Console.Error.WriteLine($"Database not found at {dbFullPath}");
                return 2;
            }

            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbFullPath}");
            conn.Open();

            string query = "SELECT Id, Detalles, IFNULL(DetallesEnc, '') AS DetallesEnc, PrevHash, Hash FROM Auditoria ORDER BY Id ASC";
            using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            string expectedPrev = string.Empty;
            int firstMismatchId = -1;
            var errors = new System.Collections.Generic.List<string>();

            while (reader.Read())
            {
                int id = Convert.ToInt32(reader["Id"]);
                string detalles = reader["Detalles"]?.ToString() ?? string.Empty;
                string prevHash = reader["PrevHash"]?.ToString() ?? string.Empty;
                string hash = reader["Hash"]?.ToString() ?? string.Empty;

                if (!string.Equals(prevHash ?? string.Empty, expectedPrev ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Mismatch prevHash at Id={id}: expected {expectedPrev}, found {prevHash}");
                    if (firstMismatchId == -1) firstMismatchId = id;
                }

                string recalculated = ComputeSha256Hex((prevHash ?? string.Empty) + (detalles ?? string.Empty));
                if (!string.Equals(recalculated, hash ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Hash mismatch at Id={id}: expected {hash}, recalculated {recalculated}");
                    if (firstMismatchId == -1) firstMismatchId = id;
                }

                expectedPrev = hash ?? string.Empty;
            }

            if (errors.Count == 0)
            {
                Console.WriteLine("No integrity errors detected.");
                return 0;
            }

            // Build report: include surrounding rows
            int idCenter = firstMismatchId > 0 ? firstMismatchId : 1;
            int windowBefore = 5;
            int windowAfter = 5;
            int fromId = Math.Max(1, idCenter - windowBefore);
            int toId = idCenter + windowAfter;

            string surroundQuery = "SELECT Id, UsuarioAdmin, Accion, Fechahora, Detalles, DetallesEnc, PrevHash, Hash, Signature, KeyVersion, KeyVersionEnc FROM Auditoria WHERE Id BETWEEN @from AND @to ORDER BY Id";
            using var cmd2 = new Microsoft.Data.Sqlite.SqliteCommand(surroundQuery, conn);
            cmd2.Parameters.AddWithValue("@from", fromId);
            cmd2.Parameters.AddWithValue("@to", toId);

            var surrounding = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
            using var rdr2 = cmd2.ExecuteReader();
            while (rdr2.Read())
            {
                var row = new System.Collections.Generic.Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < rdr2.FieldCount; i++)
                {
                    string name = rdr2.GetName(i);
                    object val = rdr2.IsDBNull(i) ? null : rdr2.GetValue(i);
                    row[name] = val ?? string.Empty;
                }
                surrounding.Add(row);
            }

            var report = new
            {
                GeneratedAt = DateTime.Now.ToString("o"),
                FirstMismatchId = firstMismatchId,
                Errors = errors,
                SurroundingRows = surrounding
            };

            string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClinicaLongevidadApp", "AuditIntegrityReports");
            Directory.CreateDirectory(outDir);
            string outPath = Path.Combine(outDir, $"IntegrityReport_{DateTime.Now:yyyyMMdd_HHmmss}_id{firstMismatchId}.json");
            var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(outPath, System.Text.Json.JsonSerializer.Serialize(report, opts));

            Console.WriteLine($"Integrity diagnostic report written to: {outPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error generating diagnostic report: " + ex);
            return 2;
        }
    }

    private static string ComputeSha256Hex(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input ?? string.Empty);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
