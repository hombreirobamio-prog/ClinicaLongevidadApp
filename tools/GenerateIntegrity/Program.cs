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
                var dbPath = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "audit_ci.db");
                var reportPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "IntegrityReport_CI.json");

                var conn = $"Data Source={dbPath}";
                var svc = new AuditoriaService(conn);

                // First produce quick diagnostics (summary + CSV of problematic rows)
                var quickDir = svc.GenerateQuickDiagnostics();

                var errors = svc.VerifyIntegrity();

                var report = new
                {
                    GeneratedAt = DateTime.UtcNow.ToString("o"),
                    Database = dbPath,
                    QuickDiagnosticsPath = quickDir,
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
