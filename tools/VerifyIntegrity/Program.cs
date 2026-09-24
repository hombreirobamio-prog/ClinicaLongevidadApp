using System;
using ClinicaLongevidadApp.Services;

class Program
{
    static int Main(string[] args)
    {
        try
        {
            string conn;
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])) conn = args[0];
            else
            {
                var dbPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicaLongevidad.db");
                conn = $"Data Source={dbPath}";
            }

            var svc = new AuditoriaService(conn);
            Console.WriteLine("Running VerifyIntegrity()...");
            var errors = svc.VerifyIntegrity();
            Console.WriteLine($"Errors count: {errors?.Count ?? 0}");
            if (errors != null && errors.Count > 0)
            {
                var take = Math.Min(20, errors.Count);
                Console.WriteLine($"First {take} errors:");
                for (int i = 0; i < take; i++) Console.WriteLine(errors[i]);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("VerifyIntegrity run failed: " + ex.Message);
            return 2;
        }
    }
}
