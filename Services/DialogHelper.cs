using System;
using System.Linq;
using System.Windows;

namespace ClinicaLongevidadApp.Services
{
    /// <summary>
    /// Helper to show dialogs. When running under unit tests (xUnit/vstest) it will avoid
    /// showing blocking MessageBox dialogs and will log instead.
    /// </summary>
    public static class DialogHelper
    {
        private static bool RunningUnderTest()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                return assemblies.Any(a =>
                    (a.FullName ?? string.Empty).IndexOf("xunit", StringComparison.OrdinalIgnoreCase) >= 0
                    || (a.FullName ?? string.Empty).IndexOf("microsoft.visualstudio.testplatform", StringComparison.OrdinalIgnoreCase) >= 0
                    || (a.FullName ?? string.Empty).IndexOf("nunit", StringComparison.OrdinalIgnoreCase) >= 0);
            }

        private static bool ShouldSuppressDialogs()
        {
            try
            {
                if (RunningUnderTest()) return true;
                var silent = Environment.GetEnvironmentVariable("SILENT_MODE");
                if (string.Equals(silent, "1", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            return false;
        }
            catch
            {
                return false;
            }
        }

        public static void ShowInfo(string title, string message)
        {
            if (ShouldSuppressDialogs())
            {
                try { AuditLogHelper.Info(title, message); } catch { }
                return;
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void ShowWarning(string title, string message)
        {
            if (ShouldSuppressDialogs())
            {
                try { LogService.Warning(title, message); } catch { }
                return;
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void ShowError(string title, string message)
        {
            if (ShouldSuppressDialogs())
            {
                try { LogService.Error(title, message); } catch { }
                return;
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Show a Yes/No confirmation. When dialogs are suppressed (tests or SILENT_MODE)
        /// this returns true to allow automated flows to proceed.
        /// </summary>
        public static bool ConfirmYesNo(string title, string message)
        {
            try
            {
                if (ShouldSuppressDialogs())
                {
                    try { AuditLogHelper.Info(title, "Auto-confirm (suppressed dialogs): " + message); } catch { }
                    return true;
                }
            }
            catch { }

            var res = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return res == MessageBoxResult.Yes;
        }
    }
}
