using ClinicaLongevidadApp.Commands;
using ClinicaLongevidadApp.Views;

namespace ClinicaLongevidadApp.ViewModels
{
    public class VolverViewModel
    {
        public RelayCommand VolverCommand { get; }

        public VolverViewModel()
        {
            VolverCommand = new RelayCommand(_ =>
            {
                App.DashboardViewModel!.CurrentView = new HomeView();
            });
        }
    }
}
