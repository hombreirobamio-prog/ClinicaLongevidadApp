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
        private readonly string _connectionString;

        public ObservableCollection<AuditQueueRowDto> Pending { get; } = new();
        public ObservableCollection<AuditQueueRowDto> DeadLetter { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand RequeueCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand GenerateDiagnosticsCommand { get; }

        private AuditQueueRowDto? _selectedDead;
        public AuditQueueRowDto? SelectedDead
        {
            get => _selectedDead;
            set
            {
                if (_selectedDead == value) return;
                _selectedDead = value;
                OnPropertyChanged(nameof(SelectedDead));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public AuditAdminViewModel(string connectionString)
        {
            _connectionString = connectionString;
            _service = new AuditAdminService(connectionString);
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            RequeueCommand = new RelayCommand(async _ => await RequeueSelectedAsync());
            DeleteCommand = new RelayCommand(async _ => await DeleteSelectedAsync());
            GenerateDiagnosticsCommand = new RelayCommand(async _ => await GenerateDiagnosticsAsync());
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
            if (SelectedDead == null)
            {
                DialogHelper.ShowWarning("Cola de auditoría", "Seleccione primero un registro de Dead Letter.");
                return;
            }
            var id = SelectedDead.Id;
            var ok = await Task.Run(() => _service.RequeueDeadLetter(id));
            if (ok) await RefreshAsync();
            else DialogHelper.ShowError("Cola de auditoría", "No se pudo completar la operación. Compruebe los permisos, el registro seleccionado y el servicio de auditoría.");
        }

        public async Task DeleteSelectedAsync()
        {
            if (SelectedDead == null)
            {
                DialogHelper.ShowWarning("Cola de auditoría", "Seleccione primero un registro de Dead Letter.");
                return;
            }
            var id = SelectedDead.Id;
            var ok = await Task.Run(() => _service.DeleteDeadLetter(id));
            if (ok) await RefreshAsync();
            else DialogHelper.ShowError("Cola de auditoría", "No se pudo completar la operación. Compruebe los permisos, el registro seleccionado y el servicio de auditoría.");
        }

        private async Task GenerateDiagnosticsAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var svc = new AuditoriaService(_connectionString);
                    var quick = svc.GenerateQuickDiagnostics();
                    var report = svc.GenerateIntegrityDiagnosticReport();
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine("Diagnóstico completado.");
                            if (!string.IsNullOrWhiteSpace(quick)) sb.AppendLine("Ficheros rápidos escritos en: " + quick);
                            if (!string.IsNullOrWhiteSpace(report)) sb.AppendLine("Informe de integridad escrito en: " + report);
                            System.Windows.MessageBox.Show(sb.ToString(), "Audit Diagnostics", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                        }
                        catch { }
                    });
                });
            }
            catch (Exception ex)
            {
                try { System.Windows.MessageBox.Show("Error generando diagnósticos: " + ex.Message, "Audit Diagnostics", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error); } catch { }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
