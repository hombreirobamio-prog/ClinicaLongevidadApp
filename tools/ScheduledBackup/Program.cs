using ClinicaLongevidadApp.Services;

namespace ScheduledBackup;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h" or "/?")
        {
            Console.WriteLine("Uso: ScheduledBackup");
            Console.WriteLine("Crea una copia autenticada de la base central de Clínica Longevidad.");
            return 0;
        }

        try
        {
            var databasePath = Path.Combine(AppPaths.BaseDir, "ClinicaLongevidad.db");
            if (!File.Exists(databasePath))
                throw new InvalidOperationException("No existe la base central configurada para la copia.");

            using var backupService = new BackupService();
            var created = backupService.TriggerImmediateBackup($"Data Source={databasePath}", AppPaths.BackupsDir);
            if (string.IsNullOrWhiteSpace(created))
                throw new InvalidOperationException("No se pudo crear la copia autenticada.");

            Console.WriteLine("Copia autenticada creada: " + created);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error de copia programada: " + ex.Message);
            return 4;
        }
    }
}
