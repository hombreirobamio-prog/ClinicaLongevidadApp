using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ClinicaLongevidadApp.Services;

namespace ClinicaLongevidadApp.ViewModels
{
    public class AuditAdminViewModel : INotifyPropertyChanged
    {
        private readonly AuditAdminService _service;

        public ObservableCollection<AuditQueueRowDto> Pending { get; } = new();
        public ObservableCollection<AuditQueueRowDto> DeadLetter { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand RequeueCommand { get; }
        public ICommand DeleteCommand { get; }

        private AuditQueueRowDto? _selectedDead;
        public AuditQueueRowDto? SelectedDead
        {
            get => _selectedDead;
            set { _selectedDead = value; OnPropertyChanged(nameof(SelectedDead)); }
        }

        public AuditAdminViewModel(string connectionString)
        {
            _service = new AuditAdminService(connectionString);
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            RequeueCommand = new RelayCommand(async _ => await RequeueSelectedAsync(), _ => SelectedDead != null);
            DeleteCommand = new RelayCommand(async _ => await DeleteSelectedAsync(), _ => SelectedDead != null);
        }

        public async Task RefreshAsync()
        {
            await Task.Run(() =>
            {
                var pending = _service.GetPending(200);
                var dead = _service.GetDeadLetter(200);

                App.Current.Dispatcher.Invoke(() =>
                {
                    Pending.Clear();
                    foreach (var p in pending) Pending.Add(p);
                    DeadLetter.Clear();
                    foreach (var d in dead) DeadLetter.Add(d);
                });
            });
        }

        public async Task RequeueSelectedAsync()
        {
            if (SelectedDead == null) return;
            var id = SelectedDead.Id;
            var ok = await Task.Run(() => _service.RequeueDeadLetter(id));
            if (ok) await RefreshAsync();
        }

        public async Task DeleteSelectedAsync()
        {
            if (SelectedDead == null) return;
            var id = SelectedDead.Id;
            var ok = await Task.Run(() => _service.DeleteDeadLetter(id));
            if (ok) await RefreshAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
