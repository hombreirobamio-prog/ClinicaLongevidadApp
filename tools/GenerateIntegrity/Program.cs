using System;
using System.IO;
using System.Text.Json;
using ClinicaLongevidadApp.Services;

namespace GenerateIntegrityTool
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                string dbPath;
                if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
                {
                    // Default to centralized DB location used by the app
                    dbPath = Path.Combine(ClinicaLongevidadApp.Services.AppPaths.BaseDir, "ClinicaLongevidad.db");
                }
                else
                {
                    dbPath = args[0];
                }

                var reportPath = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
                    ? args[1]
                    : Path.Combine(ClinicaLongevidadApp.Services.AppPaths.ReportsDir, "IntegrityReport_CI.json");
                // Ensure report dir exists
                try { Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? Directory.GetCurrentDirectory()); } catch { }

                var conn = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
                var svc = new AuditoriaService(conn, initializeSchema: false);
                if (!svc.IsInitialized) return 2;

                // First produce quick diagnostics (summary + CSV of problematic rows)

                var errors = svc.VerifyIntegrity();

                var report = new
                {
                    GeneratedAt = DateTime.UtcNow.ToString("o"),
                    Database = dbPath,
                    ErrorCount = errors?.Count ?? 0,
                    Errors = errors
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(reportPath, JsonSerializer.Serialize(report, options));
                Console.WriteLine($"Integrity report written to {reportPath}");
                if (errors != null && errors.Count > 0)
                {
                    Console.Error.WriteLine($"Integrity check failed: {errors.Count} errors found. See {reportPath}.");
                    return 3;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to generate integrity report: " + ex.Message);
                return 2;
            }
        }
    }
}
