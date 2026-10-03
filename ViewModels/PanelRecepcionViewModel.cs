using ClinicaLongevidadApp.Core;
using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ClinicaLongevidadApp.ViewModels
{
    public enum PanelRecepcionModo
    {
        Completo,
        Citas,
        Pacientes
    }


    /// <summary>
    /// ViewModel para la gestión de recepciones, pacientes y citas.
    /// Implementa la lógica de presentación del panel de recepción.
    /// </summary>
    public class PanelRecepcionViewModel : ViewModelBase, IDisposable
    {
        // ============================
        //  CONSTANTES DE ESTADO
        // ============================

        private const string ESTADO_PENDIENTE = "Pendiente";
        private const string ESTADO_CONFIRMADA = "Confirmada";
        private const string ESTADO_SALA_ESPERA = "Sala espera";
        private const string ESTADO_EN_CONSULTA = "En consulta";
        private const string ESTADO_FINALIZADA = "Finalizada";
        private const string ESTADO_CANCELADA = "Cancelada";
        private const string ESTADO_FACTURADA = "Facturada";
        private const string PROFESIONAL_PENDIENTE = "Pendiente de asignar";
        private const string FILTRO_TODOS = "Todos";

        // ============================
        //  PRIMER BLOQUE: PACIENTES
        // ============================

        private readonly PanelRecepcionModo _modo;
        private bool _disposed;
        private readonly IPacienteService _pacienteService;
        private readonly ICitaService _citaService;
        private readonly IAuditoriaService _auditoriaService;

        public bool MostrarSeccionPacientes => _modo != PanelRecepcionModo.Citas;
        public bool MostrarSeccionCitas => _modo != PanelRecepcionModo.Pacientes;

        private string? _nombre;
        public string? Nombre
        {
            get => _nombre;
            set
            {
                _nombre = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RequiereRegularizarProteccionDatos));
            }
        }

        private Paciente? _pacienteActual;
        private string _textoBusqueda = string.Empty;
        private Paciente? _pacienteSeleccionado;
        private DateTime _fechaCita = DateTime.Today;
        private DateTime _fechaConsultaCitas = DateTime.Today;
        private string _horaCita = string.Empty;
        private string _profesionalCita = string.Empty;
        private string _profesionalConsulta = FILTRO_TODOS;
        private string _estadoCita = ESTADO_PENDIENTE;
        private Cita? _citaSeleccionada;
        private Cita? _proximaCitaPaciente;
        private Cita? _proximaCitaSeleccionada;
        private Cita? _citaEnEdicion;

        public ObservableCollection<Paciente> Pacientes { get; } = new();
        public ObservableCollection<Cita> CitasDelDia { get; } = new();
        public ObservableCollection<Cita> HistorialCitasPaciente { get; } = new();
        public ObservableCollection<Cita> ProximasCitasPaciente { get; } = new();
        public ObservableCollection<string> HorasDisponibles { get; } = new();
        public ObservableCollection<string> Profesionales { get; } = new();
        public ObservableCollection<string> ProfesionalesFiltro { get; } = new();

        public Cita? ProximaCitaPaciente
        {
            get => _proximaCitaPaciente;
            private set
            {
                _proximaCitaPaciente = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProximaCitaPacienteTexto));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string ProximaCitaPacienteTexto
        {
            get
            {
                if (ProximasCitasPaciente.Count == 0)
                {
                    return "Sin próximas citas";
                }

                return string.Join(Environment.NewLine, ProximasCitasPaciente.Select((cita, index) =>
                {
                    string profesional = string.IsNullOrWhiteSpace(cita.Profesional)
                        ? PROFESIONAL_PENDIENTE
                        : cita.Profesional;

                    return $"{index + 1}. {cita.Fecha:dd/MM/yyyy} {cita.Hora} · {profesional} · {cita.Estado}";
                }));
            }
        }

        public DateTime FechaCita
        {
            get => _fechaCita;
            set
            {
                _fechaCita = value;
                OnPropertyChanged();
                _ = CargarHorasDisponiblesAsync();
            }
        }

        public DateTime FechaConsultaCitas
        {
            get => _fechaConsultaCitas;
            set
            {
                _fechaConsultaCitas = value;
                OnPropertyChanged();
                _ = CargarCitasDelDiaAsync();
            }
        }

        public string HoraCita
        {
            get => _horaCita;
            set
            {
                _horaCita = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string ProfesionalConsulta
        {
            get => _profesionalConsulta;
            set
            {
                _profesionalConsulta = value ?? FILTRO_TODOS;
                OnPropertyChanged();
                _ = CargarCitasDelDiaAsync();
            }
        }

        public string ProfesionalCita
        {
            get => _profesionalCita;
            set
            {
                _profesionalCita = value ?? string.Empty;
                OnPropertyChanged();
                _ = CargarHorasDisponiblesAsync();
            }
        }

        public string EstadoCita
        {
            get => _estadoCita;
            set { _estadoCita = value ?? ESTADO_PENDIENTE; OnPropertyChanged(); }
        }

        public Cita? CitaSeleccionada
        {
            get => _citaSeleccionada;
            set
            {
                _citaSeleccionada = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TieneCitaSeleccionada));
                OnPropertyChanged(nameof(PuedeConfirmar));
                OnPropertyChanged(nameof(PuedeMarcarSalaEspera));
                OnPropertyChanged(nameof(PuedeMarcarEnConsulta));
                OnPropertyChanged(nameof(PuedeMarcarFinalizada));
                OnPropertyChanged(nameof(PuedeFacturar));
                OnPropertyChanged(nameof(MostrarBotonFacturar));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public Cita? ProximaCitaSeleccionada
        {
            get => _proximaCitaSeleccionada;
            set
            {
                _proximaCitaSeleccionada = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TieneProximaCitaSeleccionada));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private static string NormalizarEstado(string? estado) => (estado ?? string.Empty).Trim();

        private static bool EstadoEs(Cita? cita, params string[] estados)
        {
            if (cita is null)
            {
                return false;
            }

            string estadoActual = NormalizarEstado(cita.Estado);
            return estados.Any(estado => string.Equals(estadoActual, estado, StringComparison.OrdinalIgnoreCase));
        }

        public bool TieneCitaSeleccionada => CitaSeleccionada is not null;
        public bool TieneProximaCitaSeleccionada => ProximaCitaSeleccionada is not null;
        public bool PuedeConfirmar => EstadoEs(CitaSeleccionada, ESTADO_PENDIENTE);

        public bool PuedeMarcarSalaEspera =>
            EstadoEs(CitaSeleccionada, ESTADO_CONFIRMADA, "Confirmado");

        public bool PuedeMarcarEnConsulta =>
            EstadoEs(CitaSeleccionada, ESTADO_SALA_ESPERA, "Sala de espera");

        public bool PuedeMarcarFinalizada =>
            EstadoEs(CitaSeleccionada, ESTADO_EN_CONSULTA);

        public bool PuedeFacturar =>
            EstadoEs(CitaSeleccionada, ESTADO_FINALIZADA, "Finalizado", "Terminado", "Terminada");

        public bool MostrarBotonFacturar => PuedeFacturar;
        public bool EstaEditandoCita => _citaEnEdicion is not null;
        public string TextoBotonCrearEditarCita => EstaEditandoCita ? "Guardar cambios" : "Aceptar";

        public string PacienteParaCita =>
            _pacienteActual?.NombreCompleto ?? "Seleccione un paciente";

        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                _textoBusqueda = value ?? string.Empty;
                OnPropertyChanged();
                FiltrarPacientes();
            }
        }

        public Paciente? PacienteSeleccionado
        {
            get => _pacienteSeleccionado;
            set
            {
                _pacienteSeleccionado = value;
                OnPropertyChanged();

                if (value is not null)
                {
                    CargarPaciente(value);
                }
                else
                {
                    ActualizarFichaPaciente();
                }
                OnPropertyChanged(nameof(PacienteParaCita));
            }
        }

        public string TextoBotonDatos =>
            _pacienteActual is null
                ? (MostrarDatosAdicionales ? "Guardar datos" : "Añadir Datos")
                : (MostrarDatosAdicionales ? "Guardar cambios" : "Editar datos");

        public bool RequiereRegularizarProteccionDatos
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Nombre) &&
                    string.IsNullOrWhiteSpace(Telefono) &&
                    string.IsNullOrWhiteSpace(Email))
                {
                    return false;
                }

                if (_pacienteActual is null)
                {
                    return false;
                }

                return string.IsNullOrWhiteSpace(Firma) ||
                       !string.Equals((ProteccionDatos ?? string.Empty).Trim(), "Acepta", StringComparison.OrdinalIgnoreCase);
            }
        }

        private string? _telefono;
        public string? Telefono
        {
            get => _telefono;
            set
            {
                _telefono = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RequiereRegularizarProteccionDatos));
            }
        }

        private string? _email;
        public string? Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RequiereRegularizarProteccionDatos));
            }
        }


        // ============================================
        //  SEGUNDO BLOQUE: DATOS ADICIONALES (OCULTOS)
        // ============================================

        private bool _mostrarDatosAdicionales;
        public bool MostrarDatosAdicionales
        {
            get => _mostrarDatosAdicionales;
            set
            {
                _mostrarDatosAdicionales = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextoBotonDatos));
            }
        }

        private string? _dni;
        public string? DNI
        {
            get => _dni;
            set { _dni = value; OnPropertyChanged(); }
        }

        private DateTime? _fechaNacimiento;
        public DateTime? FechaNacimiento
        {
            get => _fechaNacimiento;
            set { _fechaNacimiento = value; OnPropertyChanged(); }
        }

        private string? _sexo;
        public string? Sexo
        {
            get => _sexo;
            set { _sexo = value; OnPropertyChanged(); }
        }

        private string? _calle;
        public string? Calle
        {
            get => _calle;
            set { _calle = value; OnPropertyChanged(); }
        }

        private string? _numero;
        public string? Numero
        {
            get => _numero;
            set { _numero = value; OnPropertyChanged(); }
        }

        private string? _piso;
        public string? Piso
        {
            get => _piso;
            set { _piso = value; OnPropertyChanged(); }
        }

        private string? _cp;
        public string? CP
        {
            get => _cp;
            set { _cp = value; OnPropertyChanged(); }
        }

        private string? _municipio;
        public string? Municipio
        {
            get => _municipio;
            set { _municipio = value; OnPropertyChanged(); }
        }

        private string? _provincia;
        public string? Provincia
        {
            get => _provincia;
            set { _provincia = value; OnPropertyChanged(); }
        }

        private string? _proteccionDatos;
        public string? ProteccionDatos
        {
            get => _proteccionDatos;
            set
            {
                _proteccionDatos = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RequiereRegularizarProteccionDatos));
            }
        }

        private string? _firma;
        public string? Firma
        {
            get => _firma;
            set
            {
                _firma = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RequiereRegularizarProteccionDatos));
            }
        }

        private DateTime? _fechaAlta;
        public DateTime? FechaAlta
        {
            get => _fechaAlta;
            set { _fechaAlta = value; OnPropertyChanged(); }
        }


        // ============================
        //  COMANDOS
        // ============================

        private ICommand? _mostrarDatosAdicionalesCommand;
        private ICommand? _guardarDatosCommand;
        private ICommand? _limpiarBusquedaCommand;
        private ICommand? _mostrarCrearCitaCommand;
        private ICommand? _crearCitaCommand;
        private ICommand? _editarCitaCommand;
        private ICommand? _cancelarEdicionCitaCommand;
        private ICommand? _confirmarCitaCommand;
        private ICommand? _cancelarCitaCommand;
        private ICommand? _marcarSalaEsperaCommand;
        private ICommand? _marcarEnConsultaCommand;
        private ICommand? _marcarFinalizadaCommand;
        private ICommand? _facturarCitaCommand;
        private ICommand? _editarProximaCitaCommand;
        private ICommand? _eliminarProximaCitaCommand;

        public ICommand MostrarDatosAdicionalesCommand => _mostrarDatosAdicionalesCommand!;
        public ICommand GuardarDatosCommand => _guardarDatosCommand!;
        public ICommand LimpiarBusquedaCommand => _limpiarBusquedaCommand!;
        public ICommand MostrarCrearCitaCommand => _mostrarCrearCitaCommand!;
        public ICommand CrearCitaCommand => _crearCitaCommand!;
        public ICommand EditarCitaCommand => _editarCitaCommand!;
        public ICommand CancelarEdicionCitaCommand => _cancelarEdicionCitaCommand!;
        public ICommand ConfirmarCitaCommand => _confirmarCitaCommand!;
        public ICommand CancelarCitaCommand => _cancelarCitaCommand!;
        public ICommand MarcarSalaEsperaCommand => _marcarSalaEsperaCommand!;
        public ICommand MarcarEnConsultaCommand => _marcarEnConsultaCommand!;
        public ICommand MarcarFinalizadaCommand => _marcarFinalizadaCommand!;
        public ICommand FacturarCitaCommand => _facturarCitaCommand!;
        public ICommand EditarProximaCitaCommand => _editarProximaCitaCommand!;
        public ICommand EliminarProximaCitaCommand => _eliminarProximaCitaCommand!;


        // ============================
        //  CONSTRUCTOR
        // ============================

        public PanelRecepcionViewModel() : this(PanelRecepcionModo.Completo, null, null, null, true)
        {
        }

        public PanelRecepcionViewModel(PanelRecepcionModo modo, IPacienteService? pacienteService = null, ICitaService? citaService = null, IAuditoriaService? auditoriaService = null, bool autoInitialize = true)
        {
            _modo = modo;
            _pacienteService = pacienteService ?? new PacienteServiceAdapter();
            _citaService = citaService ?? new CitaServiceAdapter();
            _auditoriaService = auditoriaService ?? new AuditoriaServiceAdapter();
            _mostrarFormularioCrearCita = _modo == PanelRecepcionModo.Citas;

            try
            {
                if (autoInitialize)
                {
                    // Iniciar inicialización asíncrona (no bloqueante) para mantener UI responsiva
                    _ = InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                LogService.Error("PanelRecepcionViewModel", "Error iniciando inicialización asíncrona", ex);
                throw;
            }

            InitializeCommands();
        }

        /// <summary>
        /// Inicialización asíncrona que carga datos sin bloquear la UI.
        /// </summary>
        private async Task InitializeAsync()
        {
            try
            {
                await CargarPacientesAsync().ConfigureAwait(false);
                await CargarProfesionalesAsync().ConfigureAwait(false);
                await CargarCitasDelDiaAsync().ConfigureAwait(false);

                // Actualizar propiedad de UI en el hilo de dispatcher
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ProfesionalCita = Profesionales.FirstOrDefault() ?? string.Empty;
                });

                LogService.Info("PanelRecepcionViewModel", $"ViewModel inicializado en modo: {_modo} (async)");
            }
            catch (Exception ex)
            {
                LogService.Error("PanelRecepcionViewModel", "Error durante la inicialización asíncrona", ex);
            }
        }

        /// <summary>
        /// Inicializa todos los comandos del ViewModel.
        /// </summary>
        private void InitializeCommands()
        {
            _mostrarDatosAdicionalesCommand = new ClinicaLongevidadApp.Core.RelayCommand(_ =>
            {
                MostrarDatosAdicionales = true;
            });

            _guardarDatosCommand = new ClinicaLongevidadApp.Core.RelayCommand(_ => _ = GuardarDatosAsync());

            _limpiarBusquedaCommand = new ClinicaLongevidadApp.Core.RelayCommand(_ =>
            {
                TextoBusqueda = string.Empty;
            });

            _crearCitaCommand = new ClinicaLongevidadApp.Core.RelayCommand(_ => _ = AceptarCitaAsync());

            _editarCitaCommand = new ClinicaLongevidadApp.Core.RelayCommand(
                _ => IniciarEdicionCitaSeleccionada(),
                _ => CitaSeleccionada is not null);

            _cancelarEdicionCitaCommand = new ClinicaLongevidadApp.Core.RelayCommand(
                _ => CancelarEdicionCita(),
                _ => EstaEditandoCita);

            _confirmarCitaCommand = new ClinicaLongevidadApp.Core.RelayCommand(
                _ => _ = ConfirmarCitaSeleccionadaAsync(),
                _ => PuedeConfirmar);

            _cancelarCitaCommand = new ClinicaLongevidadApp.Core.RelayCommand(
                _ => _ = CancelarCitaSeleccionadaAsync(),
                _ => CitaSeleccionada is not null &&
                     !EstadoEs(CitaSeleccionada, ESTADO_CANCELADA, ESTADO_FINALIZADA, "Terminado", "Terminada", ESTADO_FACTURADA));

            _marcarSalaEsperaCommand = new ClinicaLongevidadApp.Core.RelayCommand(
                _ => _ = MarcarSalaEsperaSeleccionadaAsync(),
                _ => PuedeMarcarSalaEspera);

            _marcarEnConsultaCommand = new ClinicaLongevidadApp.Core.RelayCommand(
                _ => _ = MarcarEnConsultaSeleccionadaAsync(),
                _ => PuedeMarcarEnConsulta);

            _marcarFinalizadaCommand = new ClinicaLongevidadApp.Core.RelayCommand(
                _ => _ = MarcarFinalizadaSeleccionadaAsync(),
                _ => PuedeMarcarFinalizada);

            _facturarCitaCommand = new ClinicaLongevidadApp.Core.RelayCommand(
                _ => _ = FacturarCitaSeleccionadaAsync(),
                _ => PuedeFacturar);

            _editarProximaCitaCommand = new ClinicaLongevidadApp.Core.RelayCommand(
                _ => IniciarEdicionProximaCita(),
                _ => TieneProximaCitaSeleccionada);

            _eliminarProximaCitaCommand = new ClinicaLongevidadApp.Core.RelayCommand(
                _ => _ = EliminarProximaCitaSeleccionadaAsync(),
                _ => TieneProximaCitaSeleccionada);

            _mostrarCrearCitaCommand = new ClinicaLongevidadApp.Core.RelayCommand(_ =>
            {
                if (_pacienteActual is null)
                {
                    MessageBox.Show(
                        "Seleccione primero un paciente.",
                        "Crear cita",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                MostrarFormularioCrearCita = true;
                FechaCita = DateTime.Today;
                ProfesionalCita = Profesionales.FirstOrDefault() ?? string.Empty;
                HoraCita = string.Empty;

                OnPropertyChanged(nameof(PacienteParaCita));
            });
        }

        private void CargarCitasDelDia(int? citaIdSeleccionada = null)
        {
            try
            {
                int? idASeleccionar = citaIdSeleccionada ?? CitaSeleccionada?.Id;
                CitasDelDia.Clear();

                IEnumerable<Cita> citas = _citaService.ObtenerPorFecha(FechaConsultaCitas);
                
                if (!string.Equals(ProfesionalConsulta, FILTRO_TODOS, StringComparison.OrdinalIgnoreCase))
                {
                    citas = FiltrarCitasPorProfesional(citas, ProfesionalConsulta);
                }

                Cita? citaSeleccionadaActualizada = AgregarCitasYBuscarSeleccionada(citas, idASeleccionar);
                CitaSeleccionada = citaSeleccionadaActualizada;
                CargarHorasDisponibles();
                CommandManager.InvalidateRequerySuggested();

                LogService.Info("CargarCitasDelDia", $"Se cargaron {CitasDelDia.Count} citas para {FechaConsultaCitas:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                LogService.Error("CargarCitasDelDia", "Error al cargar citas del día", ex);
                MessageBox.Show(
                    $"Error al cargar citas: {ex.Message}",
                    "Cargar citas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static IEnumerable<Cita> FiltrarCitasPorProfesional(IEnumerable<Cita> citas, string profesional)
        {
            return citas.Where(cita => string.Equals(
                cita.Profesional,
                profesional,
                StringComparison.OrdinalIgnoreCase));
        }

        private Cita? AgregarCitasYBuscarSeleccionada(IEnumerable<Cita> citas, int? idASeleccionar)
        {
            Cita? citaSeleccionadaActualizada = null;

            foreach (Cita cita in citas)
            {
                CitasDelDia.Add(cita);

                if (idASeleccionar.HasValue && cita.Id == idASeleccionar.Value)
                {
                    citaSeleccionadaActualizada = cita;
                }
            }

            return citaSeleccionadaActualizada;
        }

        private void CargarProfesionales()
        {
            try
            {
                Profesionales.Clear();
                ProfesionalesFiltro.Clear();
                Profesionales.Add(PROFESIONAL_PENDIENTE);
                ProfesionalesFiltro.Add(FILTRO_TODOS);

                IEnumerable<string> profesionales = ObtenerProfesionalesActivos();

                foreach (string profesional in profesionales)
                {
                    Profesionales.Add(profesional);
                    ProfesionalesFiltro.Add(profesional);
                }

                ProfesionalesFiltro.Add(PROFESIONAL_PENDIENTE);
                LogService.Info("CargarProfesionales", $"Se cargaron {Profesionales.Count} profesionales");
            }
            catch (Exception ex)
            {
                LogService.Error("CargarProfesionales", "Error al cargar profesionales", ex);
            }
        }

        private async Task CargarProfesionalesAsync()
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;

                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        Profesionales.Clear();
                        ProfesionalesFiltro.Clear();
                        Profesionales.Add(PROFESIONAL_PENDIENTE);
                        ProfesionalesFiltro.Add(FILTRO_TODOS);
                    });
                }
                else
                {
                    Profesionales.Clear();
                    ProfesionalesFiltro.Clear();
                    Profesionales.Add(PROFESIONAL_PENDIENTE);
                    ProfesionalesFiltro.Add(FILTRO_TODOS);
                }

                var profesionales = CacheService.GetOrSet(
                    "Profesionales",
                    () => ObtenerProfesionalesActivos().ToList(),
                    durationMinutes: 60);

                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        foreach (string profesional in profesionales)
                        {
                            Profesionales.Add(profesional);
                            ProfesionalesFiltro.Add(profesional);
                        }

                        ProfesionalesFiltro.Add(PROFESIONAL_PENDIENTE);
                    });
                }
                else
                {
                    foreach (string profesional in profesionales)
                    {
                        Profesionales.Add(profesional);
                        ProfesionalesFiltro.Add(profesional);
                    }

                    ProfesionalesFiltro.Add(PROFESIONAL_PENDIENTE);
                }

                LogService.Info("CargarProfesionalesAsync", $"Se cargaron {Profesionales.Count} profesionales (async)");
            }
            catch (Exception ex)
            {
                LogService.Error("CargarProfesionalesAsync", "Error al cargar profesionales (async)", ex);
            }
        }

        public async Task CargarPacientesAsync()
        {
            try
            {
                var pacientes = await Task.Run(() => _pacienteService.ObtenerTodos()
                    .OrderBy(p => p.NombreCompleto)
                    .ToList());

                if (Application.Current?.Dispatcher != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Pacientes.Clear();
                        foreach (var paciente in pacientes)
                        {
                            Pacientes.Add(paciente);
                        }
                    });
                }
                else
                {
                    Pacientes.Clear();
                    foreach (var paciente in pacientes)
                    {
                        Pacientes.Add(paciente);
                    }
                }

                LogService.Info("CargarPacientesAsync", $"Se cargaron {Pacientes.Count} pacientes (async)");
            }
            catch (Exception ex)
            {
                LogService.Error("CargarPacientesAsync", "Error al cargar pacientes (async)", ex);
            }
        }

        private async Task CargarCitasDelDiaAsync(int? citaIdSeleccionada = null)
        {
            try
            {
                int? idASeleccionar = citaIdSeleccionada ?? CitaSeleccionada?.Id;
                var dispatcher = Application.Current?.Dispatcher;

                var citasList = await Task.Run(() => _citaService.ObtenerPorFecha(FechaConsultaCitas).ToList());

                if (!string.Equals(ProfesionalConsulta, FILTRO_TODOS, StringComparison.OrdinalIgnoreCase))
                {
                    citasList = citasList.Where(c => string.Equals(c.Profesional, ProfesionalConsulta, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        CitasDelDia.Clear();
                        Cita? citaSeleccionadaActualizada = null;

                        foreach (var cita in citasList)
                        {
                            CitasDelDia.Add(cita);
                            if (idASeleccionar.HasValue && cita.Id == idASeleccionar.Value)
                            {
                                citaSeleccionadaActualizada = cita;
                            }
                        }

                        CitaSeleccionada = citaSeleccionadaActualizada;
                    });
                }
                else
                {
                    CitasDelDia.Clear();
                    Cita? citaSeleccionadaActualizada = null;

                    foreach (var cita in citasList)
                    {
                        CitasDelDia.Add(cita);
                        if (idASeleccionar.HasValue && cita.Id == idASeleccionar.Value)
                        {
                            citaSeleccionadaActualizada = cita;
                        }
                    }

                    CitaSeleccionada = citaSeleccionadaActualizada;
                }

                // Actualizar horas disponibles en background
                await CargarHorasDisponiblesAsync();

                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() => CommandManager.InvalidateRequerySuggested());
                }
                else
                {
                    CommandManager.InvalidateRequerySuggested();
                }

                LogService.Info("CargarCitasDelDiaAsync", $"Se cargaron {CitasDelDia.Count} citas para {FechaConsultaCitas:yyyy-MM-dd} (async)");
            }
            catch (Exception ex)
            {
                LogService.Error("CargarCitasDelDiaAsync", "Error al cargar citas del día (async)", ex);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Error al cargar citas: {ex.Message}", "Cargar citas", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private async Task CargarHorasDisponiblesAsync()
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;

                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() => HorasDisponibles.Clear());
                }
                else
                {
                    HorasDisponibles.Clear();
                }

                if (string.IsNullOrWhiteSpace(ProfesionalCita))
                {
                    if (dispatcher != null)
                    {
                        await dispatcher.InvokeAsync(() => HoraCita = string.Empty);
                    }
                    else
                    {
                        HoraCita = string.Empty;
                    }

                    return;
                }

                var citas = await Task.Run(() => ObtenerCitasDisponibilidad().ToList());

                var horas = await Task.Run(() => HorarioProfesionalService.ObtenerHorasDisponibles(ProfesionalCita, FechaCita, citas).ToList());

                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        foreach (var hora in horas)
                        {
                            HorasDisponibles.Add(hora);
                        }

                        AgregarHoraEdicionSiNecesario();
                        ActualizarHoraCitaSiNecesario();
                    });
                }
                else
                {
                    foreach (var hora in horas)
                    {
                        HorasDisponibles.Add(hora);
                    }

                    AgregarHoraEdicionSiNecesario();
                    ActualizarHoraCitaSiNecesario();
                }

                LogService.Info("CargarHorasDisponiblesAsync", $"Se cargaron {HorasDisponibles.Count} horas para {ProfesionalCita} (async)");
            }
            catch (Exception ex)
            {
                LogService.Error("CargarHorasDisponiblesAsync", "Error al cargar horas disponibles (async)", ex);
            }
        }

        private static IEnumerable<string> ObtenerProfesionalesActivos()
        {
            return UsuarioService.ObtenerTodos()
                .Where(usuario => usuario.Activo &&
                    (usuario.Rol.Contains("profes", StringComparison.OrdinalIgnoreCase) ||
                     usuario.Area.Contains("profes", StringComparison.OrdinalIgnoreCase)))
                .Select(usuario => string.IsNullOrWhiteSpace(usuario.NombreCompleto)
                    ? usuario.NombreUsuario
                    : usuario.NombreCompleto)
                .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(nombre => nombre);
        }

        private void CargarHorasDisponibles()
        {
            try
            {
                HorasDisponibles.Clear();

                if (string.IsNullOrWhiteSpace(ProfesionalCita))
                {
                    HoraCita = string.Empty;
                    return;
                }

                IEnumerable<Cita> citas = ObtenerCitasDisponibilidad();

                foreach (string hora in HorarioProfesionalService.ObtenerHorasDisponibles(
                             ProfesionalCita,
                             FechaCita,
                             citas))
                {
                    HorasDisponibles.Add(hora);
                }

                AgregarHoraEdicionSiNecesario();
                ActualizarHoraCitaSiNecesario();

                LogService.Info("CargarHorasDisponibles", $"Se cargaron {HorasDisponibles.Count} horas para {ProfesionalCita}");
            }
            catch (Exception ex)
            {
                LogService.Error("CargarHorasDisponibles", "Error al cargar horas disponibles", ex);
            }
        }

        private IEnumerable<Cita> ObtenerCitasDisponibilidad()
        {
            IEnumerable<Cita> citas = _citaService.ObtenerPorFecha(FechaCita)
                .Where(cita => string.Equals(
                    cita.Profesional,
                    ProfesionalCita,
                    StringComparison.OrdinalIgnoreCase));

            if (_citaEnEdicion is not null)
            {
                citas = citas.Where(cita => cita.Id != _citaEnEdicion.Id);
            }

            return citas;
        }

        private void AgregarHoraEdicionSiNecesario()
        {
            if (_citaEnEdicion is not null &&
                _citaEnEdicion.Fecha.Date == FechaCita.Date &&
                string.Equals(_citaEnEdicion.Profesional, ProfesionalCita, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(_citaEnEdicion.Hora) &&
                !HorasDisponibles.Contains(_citaEnEdicion.Hora, StringComparer.Ordinal))
            {
                HorasDisponibles.Add(_citaEnEdicion.Hora);
            }
        }

        private void ActualizarHoraCitaSiNecesario()
        {
            // If user has already set a HoraCita, do not overwrite it blindly.
            // Only populate HoraCita from available hours when the current value
            // is empty. This prevents background refreshes from clearing a
            // user-provided value (important for unit tests and UX).
            if (string.IsNullOrWhiteSpace(HoraCita))
            {
                HoraCita = HorasDisponibles.FirstOrDefault() ?? string.Empty;
            }
        }

        private void AceptarCita()
        {
            try
            {
                if (!AsegurarPacienteParaCita())
                    return;

                if (!ValidarCitaCompleta())
                    return;

                if (FestivoService.EsFestivo(FechaCita))
                {
                    MostrarAdvertencia("Fecha festiva", "La fecha seleccionada es festiva y no permite crear citas.");
                    return;
                }

                if (_citaService.EstaOcupada(FechaCita, HoraCita, ProfesionalCita, _citaEnEdicion?.Id ?? 0))
                {
                    MostrarAdvertencia("Hora ocupada", "La hora seleccionada ya está ocupada para ese profesional.");
                    return;
                }

                GuardarCita();
            }
            catch (Exception ex)
            {
                LogService.Error("AceptarCita", "Error al aceptar cita", ex);
                MostrarError("Guardar cita", $"No se pudo guardar la cita: {ex.Message}");
            }
        }

        /// <summary>
        /// Valida que la cita tenga todos los datos requeridos.
        /// </summary>
        private bool ValidarCitaCompleta()
        {
            if (string.IsNullOrWhiteSpace(HoraCita) || string.IsNullOrWhiteSpace(ProfesionalCita))
            {
                MostrarAdvertencia("Datos incompletos", "Seleccione una hora y un profesional.");
                return false;
            }

            return true;
        }

        private static Cita CopiarCita(Cita cita) => new()
        {
            Id = cita.Id,
            PacienteId = cita.PacienteId,
            PacienteNombre = cita.PacienteNombre,
            Fecha = cita.Fecha,
            Hora = cita.Hora,
            Profesional = cita.Profesional,
            Estado = cita.Estado,
            FechaCreacion = cita.FechaCreacion
        };
        /// <summary>
        /// Guarda la cita en base de datos y actualiza la interfaz.
        /// </summary>
        private void GuardarCita()
        {
            bool estabaEditando = EstaEditandoCita;
            Cita cita;
            try
            {
                cita = _citaEnEdicion is null ? CrearNuevaCita() : CopiarCita(_citaEnEdicion);
            }
            catch (InvalidOperationException ex)
            {
                LogService.Error("GuardarCita", "Error al crear nueva cita: " + ex.Message);
                MostrarError("Crear cita", ex.Message);
                return;
            }

            cita.PacienteId = _pacienteActual!.Id;
            cita.PacienteNombre = _pacienteActual.NombreCompleto;
            cita.Fecha = FechaCita.Date;
            cita.Hora = HoraCita;
            cita.Profesional = ProfesionalCita;

            if (!estabaEditando)
            {
                cita.Estado = EstadoCita;
            }

            _citaService.Guardar(cita);
            _ = CargarCitasDelDiaAsync(cita.Id);
            ActualizarFichaPaciente();
            HoraCita = string.Empty;
            CancelarEdicionCitaInterna();
            _ = CargarHorasDisponiblesAsync();

            string mensaje = estabaEditando ? "La cita se ha actualizado correctamente." : "La cita se ha creado correctamente.";
            LogService.Info("GuardarCita", $"Cita {(estabaEditando ? "actualizada" : "creada")}: {cita.Id}");
            MostrarInformacion("Crear cita", mensaje);
        }

        /// <summary>
        /// Crea una nueva cita con datos iniciales.
        /// </summary>
        private Cita CrearNuevaCita()
        {
            if (_pacienteActual is null)
            {
                throw new InvalidOperationException("No hay ningún paciente asociado. Seleccione o cree un paciente antes de crear la cita.");
            }

            return new Cita
            {
                PacienteId = _pacienteActual.Id,
                PacienteNombre = _pacienteActual.NombreCompleto,
                Estado = EstadoCita
            };
        }

        private void IniciarEdicionCitaSeleccionada()
        {
            if (CitaSeleccionada is null)
            {
                MostrarInformacion("Editar cita", "Seleccione una cita para editar.");
                return;
            }

            try
            {
                MostrarFormularioCrearCita = true;

                Paciente? paciente = _pacienteService.ObtenerPorId(CitaSeleccionada.PacienteId);
                if (paciente is not null)
                {
                    PacienteSeleccionado = paciente;
                }

                _citaEnEdicion = CitaSeleccionada;
                FechaCita = _citaEnEdicion.Fecha.Date;
                ProfesionalCita = _citaEnEdicion.Profesional;
                EstadoCita = _citaEnEdicion.Estado;

                if (!string.IsNullOrWhiteSpace(_citaEnEdicion.Hora) &&
                    !HorasDisponibles.Contains(_citaEnEdicion.Hora, StringComparer.Ordinal))
                {
                    HorasDisponibles.Add(_citaEnEdicion.Hora);
                }

                HoraCita = _citaEnEdicion.Hora;

                NotificarCambioEdicionCita();
                CommandManager.InvalidateRequerySuggested();
                LogService.Info("IniciarEdicionCitaSeleccionada", $"Iniciada edición de cita: {CitaSeleccionada.Id}");
            }
            catch (Exception ex)
            {
                LogService.Error("IniciarEdicionCitaSeleccionada", "Error al iniciar edición de cita", ex);
                MostrarError("Editar cita", $"Error: {ex.Message}");
            }
        }

        private void CancelarEdicionCita()
        {
            CancelarEdicionCitaInterna();
            _ = CargarHorasDisponiblesAsync();
            CommandManager.InvalidateRequerySuggested();
        }

        private void CancelarEdicionCitaInterna()
        {
            _citaEnEdicion = null;
            EstadoCita = ESTADO_PENDIENTE;
            MostrarFormularioCrearCita = _modo == PanelRecepcionModo.Citas;
            NotificarCambioEdicionCita();
        }

        private void NotificarCambioEdicionCita()
        {
            OnPropertyChanged(nameof(EstaEditandoCita));
            OnPropertyChanged(nameof(TextoBotonCrearEditarCita));
        }

        private void LimpiarFormulario()
        {
            _pacienteActual = null;
            PacienteSeleccionado = null;
            Nombre = string.Empty;
            Telefono = string.Empty;
            Email = string.Empty;
            DNI = string.Empty;
            FechaNacimiento = null;
            Sexo = string.Empty;
            Calle = string.Empty;
            Numero = string.Empty;
            Piso = string.Empty;
            CP = string.Empty;
            Municipio = string.Empty;
            Provincia = string.Empty;
            ProteccionDatos = "Pendiente";
            Firma = string.Empty;
            FechaAlta = null;
            CancelarEdicionCitaInterna();
            MostrarDatosAdicionales = false;
            OnPropertyChanged(nameof(TextoBotonDatos));
            OnPropertyChanged(nameof(RequiereRegularizarProteccionDatos));
            ActualizarFichaPaciente();
        }

        private void CambiarEstadoCitaSeleccionada(string nuevoEstado, string titulo)
        {
            if (CitaSeleccionada is null)
            {
                MostrarInformacion(titulo, "Seleccione una cita.");
                return;
            }

            try
            {
                int citaId = CitaSeleccionada.Id;
                var cita = CopiarCita(CitaSeleccionada);
                cita.Estado = nuevoEstado;
                _citaService.Guardar(cita);
                _ = CargarCitasDelDiaAsync(citaId);
                ActualizarFichaPaciente();
                CommandManager.InvalidateRequerySuggested();
                LogService.Info("CambiarEstadoCitaSeleccionada", $"Estado cambiado a '{nuevoEstado}' para cita {citaId}");
            }
            catch (Exception ex)
            {
                LogService.Error("CambiarEstadoCitaSeleccionada", $"Error al cambiar estado a '{nuevoEstado}'", ex);
                MostrarError(titulo, $"Error: {ex.Message}");
            }
        }

        private void ActualizarFichaPaciente()
        {
            try
            {
                HistorialCitasPaciente.Clear();
                ProximasCitasPaciente.Clear();
                ProximaCitaPaciente = null;
                ProximaCitaSeleccionada = null;

                if (_pacienteActual is null || _pacienteActual.Id <= 0)
                {
                    return;
                }

                var citasPaciente = _citaService.ObtenerPorPaciente(_pacienteActual.Id).ToList();
                CargarHistorialCitas(citasPaciente);
                CargarProximasCitas(citasPaciente);

                OnPropertyChanged(nameof(ProximaCitaPacienteTexto));
                LogService.Info("ActualizarFichaPaciente", $"Ficha actualizada para paciente {_pacienteActual.Id}");
            }
            catch (Exception ex)
            {
                LogService.Error("ActualizarFichaPaciente", "Error al actualizar ficha del paciente", ex);
            }
        }

        private void CargarHistorialCitas(List<Cita> citasPaciente)
        {
            foreach (Cita cita in citasPaciente.Where(cita => cita.Fecha.Date < DateTime.Today).TakeLast(10).Reverse())
            {
                HistorialCitasPaciente.Add(cita);
            }
        }

        private void CargarProximasCitas(List<Cita> citasPaciente)
        {
            DateTime ahora = DateTime.Now;

            List<Cita> proximasCitas = citasPaciente
                .Where(cita => !EstadoEs(cita, ESTADO_CANCELADA) &&
                               (cita.Fecha.Date > DateTime.Today ||
                                (cita.Fecha.Date == DateTime.Today &&
                                 TimeSpan.TryParse(cita.Hora, out TimeSpan horaCita) &&
                                 cita.Fecha.Date.Add(horaCita) >= ahora)))
                .OrderBy(cita => cita.Fecha.Date)
                .ThenBy(cita => cita.Hora)
                .ToList();

            foreach (Cita cita in proximasCitas)
            {
                ProximasCitasPaciente.Add(cita);
            }

            ProximaCitaPaciente = ProximasCitasPaciente.FirstOrDefault();
            ProximaCitaSeleccionada = ProximaCitaPaciente;
        }

        private void EliminarProximaCitaSeleccionada()
        {
            if (ProximaCitaSeleccionada is null)
            {
                MostrarInformacion("Eliminar cita", "Seleccione una próxima cita para eliminar.");
                return;
            }

            MessageBoxResult respuesta = MessageBox.Show(
                "¿Desea eliminar la cita seleccionada de próximas citas?\nLa cita se marcará como Cancelada.",
                "Eliminar cita",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (respuesta != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                Cita cita = CopiarCita(ProximaCitaSeleccionada);

                cita.Estado = ESTADO_CANCELADA;
                _citaService.Guardar(cita);

                if (_citaEnEdicion is not null && _citaEnEdicion.Id == cita.Id)
                {
                    CancelarEdicionCitaInterna();
                }

                _ = CargarCitasDelDiaAsync();
                ActualizarFichaPaciente();
                _ = CargarHorasDisponiblesAsync();
                CommandManager.InvalidateRequerySuggested();
                LogService.Info("EliminarProximaCitaSeleccionada", $"Cita cancelada: {cita.Id}");
            }
            catch (Exception ex)
            {
                LogService.Error("EliminarProximaCitaSeleccionada", "Error al eliminar próxima cita", ex);
                MostrarError("Eliminar cita", $"Error: {ex.Message}");
            }
        }

        private void ConfirmarCitaSeleccionada()
        {
            if (CitaSeleccionada is null)
            {
                MostrarInformacion("Citas del día", "Seleccione una cita para confirmar.");
                return;
            }

            if (EstadoEs(CitaSeleccionada, ESTADO_CANCELADA))
            {
                MostrarAdvertencia("Citas del día", "No se puede confirmar una cita cancelada.");
                return;
            }

            if (!EstadoEs(CitaSeleccionada, ESTADO_PENDIENTE))
            {
                return;
            }

            try
            {
                var cita = CopiarCita(CitaSeleccionada);
                int citaId = cita.Id;
                cita.Estado = ESTADO_CONFIRMADA;
                _citaService.Guardar(cita);

                ValidarProteccionDatosDelPaciente(cita);

                _ = CargarCitasDelDiaAsync(citaId);
                ActualizarFichaPaciente();
                CommandManager.InvalidateRequerySuggested();
                LogService.Info("ConfirmarCitaSeleccionada", $"Cita confirmada: {citaId}");
            }
            catch (Exception ex)
            {
                LogService.Error("ConfirmarCitaSeleccionada", "Error al confirmar cita", ex);
                MostrarError("Citas del día", $"Error: {ex.Message}");
            }
        }

        private void ValidarProteccionDatosDelPaciente(Cita cita)
        {
            Paciente? paciente = PacienteService.ObtenerPorId(cita.PacienteId);
            if (paciente is not null &&
                (string.IsNullOrWhiteSpace(paciente.Firma) ||
                 !string.Equals((paciente.ProteccionDatos ?? string.Empty).Trim(), "Acepta", StringComparison.OrdinalIgnoreCase)))
            {
                LogService.Warning("ValidarProteccionDatosDelPaciente", 
                    $"Paciente {paciente.Id} sin protección de datos regularizada");
                
                MostrarAdvertencia("Protección de datos",
                    "Aviso: este paciente aún no tiene regularizada la protección de datos (firma o aceptación pendiente).");
            }
        }

        private void MarcarSalaEsperaSeleccionada()
        {
            CambiarEstadoCitaSeleccionada(ESTADO_SALA_ESPERA, "Citas del día");
        }

        private void MarcarEnConsultaSeleccionada()
        {
            CambiarEstadoCitaSeleccionada(ESTADO_EN_CONSULTA, "Citas del día");
        }

        private void MarcarFinalizadaSeleccionada()
        {
            CambiarEstadoCitaSeleccionada(ESTADO_FINALIZADA, "Citas del día");
        }

        private void CancelarCitaSeleccionada()
        {
            if (CitaSeleccionada is null)
            {
                MostrarInformacion("Citas del día", "Seleccione una cita para marcar como cancelada.");
                return;
            }

            MessageBoxResult respuesta = MessageBox.Show(
                "¿Desea marcar la cita seleccionada como cancelada?\nLa cita no se elimina: quedará registrada en el historial con estado Cancelada.",
                "Citas del día",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (respuesta != MessageBoxResult.Yes)
            {
                return;
            }

            CambiarEstadoCitaSeleccionada(ESTADO_CANCELADA, "Citas del día");
            _ = CargarHorasDisponiblesAsync();
            ActualizarFichaPaciente();
        }

        private void FacturarCitaSeleccionada()
        {
            if (CitaSeleccionada is null)
            {
                return;
            }

            MessageBoxResult respuesta = MessageBox.Show(
                "¿Desea marcar esta cita como facturada?",
                "Facturación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (respuesta != MessageBoxResult.Yes)
            {
                return;
            }

            CambiarEstadoCitaSeleccionada(ESTADO_FACTURADA, "Facturación");
        }

        private bool AsegurarPacienteParaCita()
        {
            if (_pacienteActual is not null && _pacienteActual.Id > 0)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(Nombre))
            {
                MostrarAdvertencia("Crear cita", "Seleccione un paciente o introduzca su nombre antes de crear la cita.");
                return false;
            }

            try
            {
                _pacienteActual = new Paciente
                {
                    NombreCompleto = Nombre.Trim(),
                    Telefono = Telefono?.Trim() ?? string.Empty,
                    Email = Email?.Trim() ?? string.Empty
                };

                _pacienteService.Guardar(_pacienteActual);
                FiltrarPacientes();
                ActualizarFichaPaciente();
                OnPropertyChanged(nameof(PacienteParaCita));
                OnPropertyChanged(nameof(RequiereRegularizarProteccionDatos));
                LogService.Info("AsegurarPacienteParaCita", $"Nuevo paciente creado: {_pacienteActual.Id}");

                return true;
            }
            catch (Exception ex)
            {
                LogService.Error("AsegurarPacienteParaCita", "Error al crear paciente", ex);
                MostrarError("Crear paciente", $"Error: {ex.Message}");
                return false;
            }
        }

        private void CargarPacientes()
        {
            try
            {
                Pacientes.Clear();

                foreach (Paciente paciente in _pacienteService.ObtenerTodos()
                             .OrderBy(paciente => paciente.NombreCompleto))
                {
                    Pacientes.Add(paciente);
                }

                LogService.Info("CargarPacientes", $"Se cargaron {Pacientes.Count} pacientes");
            }
            catch (Exception ex)
            {
                LogService.Error("CargarPacientes", "Error al cargar pacientes", ex);
            }
        }

        private void FiltrarPacientes()
        {
            try
            {
                string texto = TextoBusqueda.Trim();
                IEnumerable<Paciente> pacientes = _pacienteService.ObtenerTodos()
                    .Where(paciente =>
                        texto.Length == 0 ||
                        (paciente.NombreCompleto ?? string.Empty).Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                        (paciente.DNI ?? string.Empty).Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                        (paciente.Telefono ?? string.Empty).Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                        (paciente.Email ?? string.Empty).Contains(texto, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(paciente => paciente.NombreCompleto);

                Pacientes.Clear();
                foreach (Paciente paciente in pacientes)
                {
                    Pacientes.Add(paciente);
                }

                AuditLogHelper.Info("FiltrarPacientes", $"Filtrado completado: {Pacientes.Count} pacientes encontrados");
            }
            catch (Exception ex)
            {
                AuditLogHelper.Error("FiltrarPacientes", "Error al filtrar pacientes", ex);
            }
        }

        private void CargarPaciente(Paciente paciente)
        {
            try
            {
                _pacienteActual = paciente;
                OnPropertyChanged(nameof(TextoBotonDatos));
                Nombre = paciente.NombreCompleto;
                Telefono = paciente.Telefono;
                Email = paciente.Email;
                DNI = paciente.DNI;
                FechaNacimiento = paciente.FechaNacimiento;
                Sexo = paciente.Sexo;
                Calle = paciente.Calle;
                Numero = paciente.Numero;
                Piso = paciente.Piso;
                CP = paciente.CP;
                Municipio = paciente.Municipio;
                Provincia = paciente.Provincia;
                ProteccionDatos = paciente.ProteccionDatos;
                Firma = paciente.Firma;
                FechaAlta = paciente.FechaAlta;
                OnPropertyChanged(nameof(RequiereRegularizarProteccionDatos));
                ActualizarFichaPaciente();
                AuditLogHelper.Info("CargarPaciente", $"Paciente cargado: {paciente.Id}");
            }
            catch (Exception ex)
            {
                AuditLogHelper.Error("CargarPaciente", "Error al cargar paciente", ex);
            }
        }

        private void GuardarDatos()
        {
            if (!MostrarDatosAdicionales)
            {
                MostrarDatosAdicionales = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(Nombre))
            {
                MostrarAdvertencia("Datos del paciente", "El nombre y los apellidos son obligatorios.");
                return;
            }

            try
            {
                // Usar una copia local para evitar estados compartidos nulos durante pruebas o condiciones de carrera
                var pacienteLocal = _pacienteActual ?? new Paciente();
                pacienteLocal.NombreCompleto = Nombre.Trim();
                pacienteLocal.Telefono = Telefono?.Trim() ?? string.Empty;
                pacienteLocal.Email = Email?.Trim() ?? string.Empty;
                pacienteLocal.DNI = DNI?.Trim() ?? string.Empty;
                pacienteLocal.FechaNacimiento = FechaNacimiento;
                pacienteLocal.Sexo = Sexo?.Trim() ?? string.Empty;
                pacienteLocal.Calle = Calle?.Trim() ?? string.Empty;
                pacienteLocal.Numero = Numero?.Trim() ?? string.Empty;
                pacienteLocal.Piso = Piso?.Trim() ?? string.Empty;
                pacienteLocal.CP = CP?.Trim() ?? string.Empty;
                pacienteLocal.Municipio = Municipio?.Trim() ?? string.Empty;
                pacienteLocal.Provincia = Provincia?.Trim() ?? string.Empty;
                pacienteLocal.ProteccionDatos = ProteccionDatos?.Trim() ?? string.Empty;
                pacienteLocal.Firma = Firma?.Trim() ?? string.Empty;
                pacienteLocal.FechaAlta = FechaAlta;

                // El servicio confirma el paciente y su auditoría en la misma transacción.
                _pacienteService.Guardar(pacienteLocal);

                // Solo actualizar el estado compartido si el guardado fue exitoso
                _pacienteActual = pacienteLocal;

                OnPropertyChanged(nameof(RequiereRegularizarProteccionDatos));
                FiltrarPacientes();
                LimpiarFormulario();

                MostrarInformacion("Datos del paciente", "Los datos del paciente se han guardado correctamente.");
                AuditLogHelper.Info("GuardarDatos", $"Datos guardados para paciente: {_pacienteActual?.Id}");
            }
            catch (Exception ex)
            {
                AuditLogHelper.Error("GuardarDatos", "Error al guardar datos del paciente", ex);
                MostrarError("Datos del paciente", $"No se pudieron guardar los datos: {ex.Message}");
            }
        }

        private void IniciarEdicionProximaCita()
        {
            Cita? cita = ProximaCitaSeleccionada ?? ProximaCitaPaciente;
            if (cita is null)
            {
                MostrarInformacion("Editar cita", "Seleccione una próxima cita para editar.");
                return;
            }

            try
            {
                MostrarFormularioCrearCita = true;

                Paciente? paciente = _pacienteService.ObtenerPorId(cita.PacienteId);
                if (paciente is not null)
                {
                    PacienteSeleccionado = paciente;
                }

                _citaEnEdicion = cita;
                FechaCita = cita.Fecha.Date;
                ProfesionalCita = cita.Profesional;
                EstadoCita = cita.Estado;

                if (!string.IsNullOrWhiteSpace(cita.Hora) &&
                    !HorasDisponibles.Contains(cita.Hora, StringComparer.Ordinal))
                {
                    HorasDisponibles.Add(cita.Hora);
                }

                HoraCita = cita.Hora;

                NotificarCambioEdicionCita();
                CommandManager.InvalidateRequerySuggested();
                AuditLogHelper.Info("IniciarEdicionProximaCita", $"Iniciada edición de próxima cita: {cita.Id}");
            }
            catch (Exception ex)
            {
                AuditLogHelper.Error("IniciarEdicionProximaCita", "Error al iniciar edición de próxima cita", ex);
                MostrarError("Editar cita", $"Error: {ex.Message}");
            }
        }

        // ============================
        //  WRAPPERS ASYNC PARA COMANDOS
        // ============================

        public async Task AceptarCitaAsync()
        {
            if (Application.Current?.Dispatcher != null)
                await Application.Current.Dispatcher.InvokeAsync(AceptarCita);
            else
                AceptarCita();
        }

        public async Task GuardarDatosAsync()
        {
            if (Application.Current?.Dispatcher != null)
                await Application.Current.Dispatcher.InvokeAsync(GuardarDatos);
            else
                GuardarDatos();
        }

        public async Task ConfirmarCitaSeleccionadaAsync()
        {
            if (Application.Current?.Dispatcher != null)
                await Application.Current.Dispatcher.InvokeAsync(ConfirmarCitaSeleccionada);
            else
                ConfirmarCitaSeleccionada();
        }

        public async Task CancelarCitaSeleccionadaAsync()
        {
            if (Application.Current?.Dispatcher != null)
                await Application.Current.Dispatcher.InvokeAsync(CancelarCitaSeleccionada);
            else
                CancelarCitaSeleccionada();
        }

        public async Task MarcarSalaEsperaSeleccionadaAsync()
        {
            if (Application.Current?.Dispatcher != null)
                await Application.Current.Dispatcher.InvokeAsync(MarcarSalaEsperaSeleccionada);
            else
                MarcarSalaEsperaSeleccionada();
        }

        public async Task MarcarEnConsultaSeleccionadaAsync()
        {
            if (Application.Current?.Dispatcher != null)
                await Application.Current.Dispatcher.InvokeAsync(MarcarEnConsultaSeleccionada);
            else
                MarcarEnConsultaSeleccionada();
        }

        public async Task MarcarFinalizadaSeleccionadaAsync()
        {
            if (Application.Current?.Dispatcher != null)
                await Application.Current.Dispatcher.InvokeAsync(MarcarFinalizadaSeleccionada);
            else
                MarcarFinalizadaSeleccionada();
        }

        public async Task FacturarCitaSeleccionadaAsync()
        {
            if (Application.Current?.Dispatcher != null)
                await Application.Current.Dispatcher.InvokeAsync(FacturarCitaSeleccionada);
            else
                FacturarCitaSeleccionada();
        }

        public async Task EliminarProximaCitaSeleccionadaAsync()
        {
            if (Application.Current?.Dispatcher != null)
                await Application.Current.Dispatcher.InvokeAsync(EliminarProximaCitaSeleccionada);
            else
                EliminarProximaCitaSeleccionada();
        }

        // ============================
        //  MÉTODOS AUXILIARES DE DIÁLOGO
        // ============================

        private static void MostrarInformacion(string titulo, string mensaje)
        {
            // Use DialogHelper to avoid blocking MessageBox during unit tests
            Services.DialogHelper.ShowInfo(titulo, mensaje);
        }

        private static void MostrarAdvertencia(string titulo, string mensaje)
        {
            Services.DialogHelper.ShowWarning(titulo, mensaje);
        }

        private static void MostrarError(string titulo, string mensaje)
        {
            Services.DialogHelper.ShowError(titulo, mensaje);
        }

        private bool _mostrarFormularioCrearCita;

        public bool MostrarFormularioCrearCita
        {
            get => _mostrarFormularioCrearCita;
            set
            {
                _mostrarFormularioCrearCita = value;
                OnPropertyChanged();
            }
        }


        // ============================
        //  IMPLEMENTACIÓN IDisposable
        // ============================

        /// <summary>
        /// Libera los recursos del ViewModel.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Libera los recursos del ViewModel.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Limpiar recursos administrados
                Pacientes?.Clear();
                CitasDelDia?.Clear();
                HistorialCitasPaciente?.Clear();
                ProximasCitasPaciente?.Clear();
                HorasDisponibles?.Clear();
                Profesionales?.Clear();
                ProfesionalesFiltro?.Clear();

                AuditLogHelper.Info("PanelRecepcionViewModel", "ViewModel disposto correctamente");
            }

            _disposed = true;
        }

        ~PanelRecepcionViewModel()
        {
            Dispose(false);
        }
    }
}
