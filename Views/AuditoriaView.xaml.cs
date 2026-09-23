using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClinicaLongevidadApp.Views
{
    public partial class AuditoriaView : UserControl
    {
        public AuditoriaView()
        {
            InitializeComponent();

            this.Unloaded += AuditoriaView_Unloaded;

            AuditoriaDataGrid.AddHandler(
                Mouse.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(
                    AuditoriaDataGrid_PreviewMouseWheel),
                true);
        }

        private void BtnOpenDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var localLogs = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicaLongevidadApp", "logs");
                var commonReports = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClinicaLongevidadApp", "AuditIntegrityReports");

                string? toOpen = null;
                if (System.IO.Directory.Exists(commonReports)) toOpen = commonReports;
                else if (System.IO.Directory.Exists(localLogs)) toOpen = localLogs;

                if (string.IsNullOrWhiteSpace(toOpen))
                {
                    Services.DialogHelper.ShowWarning("Abrir diagnósticos", "No hay carpetas de diagnósticos disponibles en este equipo.");
                    return;
                }

                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer", "\"" + toOpen + "\"") { UseShellExecute = true }); } catch (Exception ex) { Services.DialogHelper.ShowError("Abrir diagnósticos", "No se pudo abrir la carpeta: " + ex.Message); }
            }
            catch { }
        }

        private void BtnGenerarDiagnostico_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Generate quick diagnostics and integrity report for auditor.
                string? conn = null;
                try { conn = Application.Current.Properties["AuditConnectionString"] as string; } catch { }
                conn ??= Environment.GetEnvironmentVariable("AUDIT_DB") ?? string.Empty;

                if (string.IsNullOrWhiteSpace(conn) && App.AuditoriaService is null)
                {
                    Services.DialogHelper.ShowWarning("Generar diagnóstico", "Audit connection string not available.");
                    return;
                }

                // Use existing App.AuditoriaService if available, otherwise construct a local one.
                var svc = App.AuditoriaService ?? new Services.AuditoriaService(conn ?? string.Empty);

                // Run diagnostics asynchronously to avoid UI freeze
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        string? quick = null;
                        string? report = null;
                        try { quick = svc.GenerateQuickDiagnostics(); } catch { }
                        try { report = svc.GenerateIntegrityDiagnosticReport(); } catch { }

                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine("Diagnóstico completado.");
                        if (!string.IsNullOrWhiteSpace(quick)) sb.AppendLine("Ficheros rápidos escritos en: " + quick);
                        if (!string.IsNullOrWhiteSpace(report)) sb.AppendLine("Informe de integridad escrito en: " + report);

                        try
                        {
                            // Marshal to UI thread for notification and optional folder opening
                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                try { Services.DialogHelper.ShowInfo("Generar diagnóstico", sb.ToString()); } catch { }

                                // If report produced, open its folder; otherwise open quick diagnostics folder
                                try
                                {
                                    string? toOpen = null;
                                    if (!string.IsNullOrWhiteSpace(report)) toOpen = System.IO.Path.GetDirectoryName(report);
                                    else if (!string.IsNullOrWhiteSpace(quick)) toOpen = quick;

                                    if (!string.IsNullOrWhiteSpace(toOpen) && System.IO.Directory.Exists(toOpen))
                                    {
                                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer", "\"" + toOpen + "\"") { UseShellExecute = true }); } catch { }
                                    }
                                }
                                catch { }
                            });
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        try { Services.DialogHelper.ShowError("Generar diagnóstico", "Error generando diagnóstico: " + ex.Message); } catch { }
                    }
                });
            }
            catch (Exception ex)
            {
                try { Services.DialogHelper.ShowError("Generar diagnóstico", "Error abriendo administración de auditoría: " + ex.Message); } catch { }
            }
        }

        private void AuditoriaView_Unloaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (this.DataContext is ViewModels.AuditoriaViewModelV2 vm2)
                {
                    vm2.Cleanup();
                }
            }
            catch
            {
                // ignore cleanup errors
            }
        }

        private void AuditoriaDataGrid_PreviewMouseWheel(
            object sender,
            MouseWheelEventArgs e)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(
                AuditoriaDataGrid);

            if (scrollViewer is null)
            {
                return;
            }

            if (e.Delta > 0)
            {
                scrollViewer.LineUp();
            }
            else
            {
                scrollViewer.LineDown();
            }

            e.Handled = true;
        }

        private async void BtnRecentAudit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Prefer showing the last 10 inside the main panel if ViewModelV2 is present
                if (this.DataContext is ViewModels.AuditoriaViewModelV2 vm2)
                {
                    try
                    {
                        await vm2.CargarUltimosAsync(10);
                        return;
                    }
                    catch { /* fall back to opening auxiliary window */ }
                }

                // Fallback: open the separate RecentAuditWindow that shows 10
                string? conn = null;
                try { conn = Application.Current.Properties["AuditConnectionString"] as string; } catch { }
                if (string.IsNullOrWhiteSpace(conn))
                {
                    MessageBox.Show("Audit connection string not available.", "Audit Recent", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var w = new RecentAuditWindow(conn);
                w.Owner = Window.GetWindow(this);
                w.Show();
            }
            catch { }
        }

        private static T? FindVisualChild<T>(
            DependencyObject? elemento)
            where T : DependencyObject
        {
            if (elemento is null)
            {
                return null;
            }

            for (int i = 0;
                 i < VisualTreeHelper.GetChildrenCount(elemento);
                 i++)
            {
                var hijo = VisualTreeHelper.GetChild(elemento, i);

                if (hijo is T resultado)
                {
                    return resultado;
                }

                var resultadoHijo = FindVisualChild<T>(hijo);

                if (resultadoHijo is not null)
                {
                    return resultadoHijo;
                }
            }

            return null;
        }
    }
}
