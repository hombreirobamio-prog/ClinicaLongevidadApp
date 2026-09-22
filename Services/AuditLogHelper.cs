using System;
using System.Text.Json;

namespace ClinicaLongevidadApp.Services
{
    public static class AuditLogHelper
    {
        private static bool IncludeDetails()
        {
            return string.Equals(Environment.GetEnvironmentVariable("AUDIT_INCLUDE_DETAILS_IN_LOGS"), "1", StringComparison.OrdinalIgnoreCase);
        }

        private static string SerializeDetails(object details)
        {
            try
            {
                var json = JsonSerializer.Serialize(details);
                if (json.Length > 2000) return json.Substring(0, 2000) + "...";
                return json;
            }
            catch
            {
                return "<unable to serialize details>";
            }
        }

        public static void Info(string category, string message, object? details = null)
        {
            var msg = message;
            if (details != null)
            {
                if (IncludeDetails()) msg += " | details=" + SerializeDetails(details);
                else msg += " | details=<REDACTED>";
            }
            LogService.Info(category, msg);
        }

        public static void Warning(string category, string message, object? details = null)
        {
            var msg = message;
            if (details != null)
            {
                if (IncludeDetails()) msg += " | details=" + SerializeDetails(details);
                else msg += " | details=<REDACTED>";
            }
            LogService.Warning(category, msg);
        }

        public static void Error(string category, string message, Exception? ex = null, object? details = null)
        {
            var msg = message;
            if (details != null)
            {
                if (IncludeDetails()) msg += " | details=" + SerializeDetails(details);
                else msg += " | details=<REDACTED>";
            }
            LogService.Error(category, msg, ex);
        }
    }
}
