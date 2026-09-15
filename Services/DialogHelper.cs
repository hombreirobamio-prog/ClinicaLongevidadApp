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
            catch
            {
                return false;
            }
        }

        public static void ShowInfo(string title, string message)
        {
            if (RunningUnderTest())
            {
                try { AuditLogHelper.Info(title, message); } catch { }
                return;
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void ShowWarning(string title, string message)
        {
            if (RunningUnderTest())
            {
                try { LogService.Warning(title, message); } catch { }
                return;
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void ShowError(string title, string message)
        {
            if (RunningUnderTest())
            {
                try { LogService.Error(title, message); } catch { }
                return;
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
