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

        private void AuditoriaView_Unloaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (this.DataContext is ViewModels.AuditoriaViewModel vm)
                {
                    vm.Cleanup();
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