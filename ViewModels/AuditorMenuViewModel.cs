using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ClinicaLongevidadApp.ViewModels
{
    public sealed class AuditorMenuViewModel : INotifyPropertyChanged
    {
        private string? _selectedZipPath;
        public string? SelectedZipPath
        {
            get => _selectedZipPath;
            set { _selectedZipPath = value; OnPropertyChanged(); }
        }

        public ICommand? SelectZipCommand { get; set; }
        public ICommand? VerifyManifestCommand { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
