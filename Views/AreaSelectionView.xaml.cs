using System.Windows.Controls;
using ClinicaLongevidadApp.ViewModels;

namespace ClinicaLongevidadApp.Views
{
    public partial class AreaSelectionView : UserControl
    {
        public AreaSelectionView()
        {
            InitializeComponent();
            DataContext = new AreaSelectionViewModel();   // ← ESTA LÍNEA ES IMPRESCINDIBLE
        }
    }
}
