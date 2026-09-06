using System.Windows;

namespace ClinicaLongevidadApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (App.DashboardViewModel is not null)
            {
                DataContext = App.DashboardViewModel;
            }
        }
    }
}