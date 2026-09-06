using System.Windows.Controls;
using ClinicaLongevidadApp.ViewModels;

namespace ClinicaLongevidadApp.Views
{
    public partial class DashboardRecepcionView : UserControl
    {
        public DashboardRecepcionView()
        {
            InitializeComponent();

            DataContext = new DashboardRecepcionViewModel();
        }
    }
}