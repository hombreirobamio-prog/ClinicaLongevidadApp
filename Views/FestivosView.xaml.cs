using System.Windows.Controls;
using ClinicaLongevidadApp.ViewModels;

namespace ClinicaLongevidadApp.Views
{
    public partial class FestivosView : UserControl
    {
        public FestivosView()
        {
            InitializeComponent();
            DataContext = new FestivosViewModel();
        }
    }
}
