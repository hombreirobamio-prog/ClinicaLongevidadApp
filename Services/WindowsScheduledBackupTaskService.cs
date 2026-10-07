using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace ClinicaLongevidadApp.Services
{
    /// <summary>
    /// Manages the per-user Windows task that runs the authenticated backup
    /// outside the WPF process.
    /// </summary>
    public static class WindowsScheduledBackupTaskService
    {
        public const string TaskName = "ClinicaLongevidadApp\\DailyAuthenticatedBackup";

        public static string RunnerPath => Path.Combine(AppPaths.BaseDir, "scheduled-backup-runner", "ScheduledBackup.exe");

        public static bool IsRunnerInstalled => File.Exists(RunnerPath);

        public static bool IsTaskInstalled()
        {
            var result = RunSchtasks($"/Query /TN \"{TaskName}\"");
            return result.ExitCode == 0;
        }

        public static void ScheduleDaily(TimeSpan localTime)
        {
            if (!IsRunnerInstalled)
                throw new InvalidOperationException("No está instalado el ejecutor independiente de copias.");

            var time = localTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            var result = RunSchtasks($"/Create /TN \"{TaskName}\" /TR \"\\\"{RunnerPath}\\\"\" /SC DAILY /ST {time} /RL LIMITED /F");
            if (result.ExitCode != 0)
                throw new InvalidOperationException("Windows no pudo programar la copia diaria: " + result.Output.Trim());

            var settings = RunPowerShell(
                "$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable; " +
                "Set-ScheduledTask -TaskName 'DailyAuthenticatedBackup' -TaskPath '\\ClinicaLongevidadApp\\' -Settings $settings | Out-Null");
            if (settings.ExitCode != 0)
                throw new InvalidOperationException("Windows no pudo aplicar las opciones de la copia diaria: " + settings.Output.Trim());
        }

        public static void CancelDaily()
        {
            var result = RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
            if (result.ExitCode != 0
                && !result.Output.Contains("cannot find", StringComparison.OrdinalIgnoreCase)
                && !result.Output.Contains("no se puede encontrar", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Windows no pudo cancelar la copia diaria: " + result.Output.Trim());
        }

        private static (int ExitCode, string Output) RunSchtasks(string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }) ?? throw new InvalidOperationException("No se pudo iniciar el Programador de tareas de Windows.");

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output);
        }

        private static (int ExitCode, string Output) RunPowerShell(string command)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command \"" + command.Replace("\"", "`\"") + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }) ?? throw new InvalidOperationException("No se pudo configurar el Programador de tareas de Windows.");

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output);
        }
    }
}
