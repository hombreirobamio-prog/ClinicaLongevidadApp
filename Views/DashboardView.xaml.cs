using System.Windows.Controls;

namespace ClinicaLongevidadApp.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();

            // ⭐ IMPORTANTE:
            // En la versión que funcionaba NO había DataContext aquí.
            // El DataContext lo ponía MainWindow y se heredaba correctamente.
        }
    }
}
