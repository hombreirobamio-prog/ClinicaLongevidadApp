using System;
using System.IO;

namespace ClinicaLongevidadApp.Services
{
    /// <summary>
    /// Servicio centralizado para registrar eventos y errores de la aplicación.
    /// </summary>
    public static class LogService
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClinicaLongevidadApp",
            "Logs");

        static LogService()
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }

        /// <summary>
        /// Registra un evento informativo.
        /// </summary>
        public static void Info(string category, string message)
        {
            Log("INFO", category, message, null);
        }

        /// <summary>
        /// Registra un evento de advertencia.
        /// </summary>
        public static void Warning(string category, string message)
        {
            Log("WARN", category, message, null);
        }

        /// <summary>
        /// Registra un error con excepción opcional.
        /// </summary>
        public static void Error(string category, string message, Exception? ex = null)
        {
            Log("ERROR", category, message, ex);
        }

        private static void Log(string level, string category, string message, Exception? ex)
        {
            try
            {
                string logFileName = Path.Combine(
                    LogDirectory,
                    $"log_{DateTime.Now:yyyy-MM-dd}.txt");

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{category}] {message}";

                if (ex != null)
                {
                    logEntry += Environment.NewLine + $"Exception: {ex}";
                }

                lock (LogDirectory)
                {
                    File.AppendAllText(logFileName, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Evitar excepciones en el sistema de logging
            }
        }
    }
}
