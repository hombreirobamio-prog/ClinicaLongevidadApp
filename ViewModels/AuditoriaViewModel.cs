using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using ClinicaLongevidadApp.Commands;
using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using Microsoft.Win32;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ClinicaLongevidadApp.ViewModels
{
    public class AuditoriaViewModel : BaseViewModel
    {
        private readonly Services.AuditoriaService? _auditoriaService;
        public string TextoBusqueda { get; set; } = string.Empty;
        public System.Collections.ObjectModel.ObservableCollection<string> Usuarios { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<string> Modulos { get; } = new();
        public string? UsuarioSeleccionado { get; set; }
        public string? ModuloSeleccionado { get; set; }
        public bool MostrarSoloOperacionesHerramientas { get; set; }

        public AuditoriaViewModel(Services.AuditoriaService? auditoriaService)
        {
            _auditoriaService = auditoriaService;
            // Minimal initialization; real implementation populates collections and commands
            Usuarios.Add("-- Todos --");
            Modulos.Add("-- Todos --");
        }

        public void Cleanup()
        {
            // Placeholder for cleanup actions (timers, subscriptions)
        }
    }
}
