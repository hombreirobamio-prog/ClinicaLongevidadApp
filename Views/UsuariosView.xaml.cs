using System.Windows.Controls;
using ClinicaLongevidadApp.ViewModels;

namespace ClinicaLongevidadApp.Views
{
    public partial class UsuariosView : UserControl
    {
        public UsuariosView()
        {
            InitializeComponent();
            DataContext = new UsuariosViewModel();
        }
    }
}