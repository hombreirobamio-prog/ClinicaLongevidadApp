using System.Windows.Controls;

namespace ClinicaLongevidadApp.Views
{
    public partial class SidebarView : UserControl
    {
        public SidebarView()
        {
            InitializeComponent();

            // ⭐ IMPORTANTE:
            // NO poner DataContext aquí.
            // Sidebar usa el DataContext del DashboardView.
        }
    }
}
