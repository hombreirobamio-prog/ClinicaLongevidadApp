using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ClinicaLongevidadApp.Commands;
using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;

namespace ClinicaLongevidadApp.ViewModels
{
    public class FestivosViewModel : BaseViewModel
    {
        private Festivo? _festivoSeleccionado;
        private DateTime _fechaFestivo = DateTime.Today;
        private string _nombreFestivo = string.Empty;
        private string _tipoFestivo = "Nacional";

        public ObservableCollection<Festivo> Festivos { get; } = [];
        public ObservableCollection<string> TiposFestivo { get; } =
        [
            "Nacional",
            "Autonomico",
            "Local"
        ];

        public Festivo? FestivoSeleccionado
        {
            get => _festivoSeleccionado;
            set
            {
                if (!SetProperty(ref _festivoSeleccionado, value))
                {
                    return;
                }

                if (value is null)
                {
                    return;
                }

                FechaFestivo = value.Fecha.Date;
                NombreFestivo = value.Nombre ?? string.Empty;
                TipoFestivo = string.IsNullOrWhiteSpace(value.Tipo)
                    ? "Nacional"
                    : value.Tipo;
            }
        }

        public DateTime FechaFestivo
        {
            get => _fechaFestivo;
            set => SetProperty(ref _fechaFestivo, value);
        }

        public string NombreFestivo
        {
            get => _nombreFestivo;
            set => SetProperty(ref _nombreFestivo, value);
        }

        public string TipoFestivo
        {
            get => _tipoFestivo;
            set => SetProperty(ref _tipoFestivo, value);
        }

        public RelayCommand GuardarFestivoCommand { get; }
        public RelayCommand EliminarFestivoCommand { get; }
        public RelayCommand ActualizarCommand { get; }

        public FestivosViewModel()
        {
            GuardarFestivoCommand = new RelayCommand(_ => GuardarFestivo());
            EliminarFestivoCommand = new RelayCommand(_ => EliminarFestivo());
            ActualizarCommand = new RelayCommand(_ => CargarFestivos());

            CargarFestivos();
        }

        private void CargarFestivos()
        {
            Festivos.Clear();

            foreach (Festivo festivo in FestivoService.ObtenerTodos()
                         .OrderBy(f => f.Fecha))
            {
                Festivos.Add(festivo);
            }
        }

        private void GuardarFestivo()
        {
            bool esEdicion = FestivoSeleccionado is not null && FestivoSeleccionado.Id > 0;
            int idEditando = FestivoSeleccionado?.Id ?? 0;

            if (Festivos.Any(f => f.Fecha.Date == FechaFestivo.Date && f.Id != idEditando))
            {
                MessageBox.Show(
                    "Ya existe un festivo para esa fecha.",
                    "Festivos",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                RegistrarAuditoriaFestivo(
                    accion: esEdicion ? "Festivo.Editar" : "Festivo.Crear",
                    ok: false,
                    usuarioAfectado: FechaFestivo.ToString("dd/MM/yyyy"),
                    detalles: AuditoriaDetallesHelper.CrearJson(
                        ("Fecha", FechaFestivo.Date.ToString("yyyy-MM-dd")),
                        ("Motivo", "Intento duplicado para fecha ya existente.")));
                return;
            }

            string nombre = string.IsNullOrWhiteSpace(NombreFestivo)
                ? "Festivo"
                : NombreFestivo.Trim();
            string tipo = string.IsNullOrWhiteSpace(TipoFestivo)
                ? "Nacional"
                : TipoFestivo;

            try
            {
                Festivo festivo = new Festivo
                {
                    Id = idEditando,
                    Fecha = FechaFestivo.Date,
                    Nombre = nombre,
                    Tipo = tipo,
                    Activo = FestivoSeleccionado?.Activo ?? true
                };

                FestivoService.Guardar(festivo);

                RegistrarAuditoriaFestivo(
                    accion: esEdicion ? "Festivo.Editar" : "Festivo.Crear",
                    ok: true,
                    usuarioAfectado: nombre,
                    detalles: AuditoriaDetallesHelper.CrearJson(
                        ("FestivoId", festivo.Id),
                        ("Fecha", festivo.Fecha.ToString("yyyy-MM-dd")),
                        ("Tipo", festivo.Tipo)));

                LimpiarFormulario();
                CargarFestivos();
            }
            catch (Exception ex)
            {
                RegistrarAuditoriaFestivo(
                    accion: esEdicion ? "Festivo.Editar" : "Festivo.Crear",
                    ok: false,
                    usuarioAfectado: nombre,
                    detalles: AuditoriaDetallesHelper.CrearJson(
                        ("FestivoId", idEditando),
                        ("Fecha", FechaFestivo.Date.ToString("yyyy-MM-dd")),
                        ("Error", ex.Message)));

                MessageBox.Show(
                    $"No se pudo guardar el festivo: {ex.Message}",
                    "Festivos",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void EliminarFestivo()
        {
            if (FestivoSeleccionado is null)
            {
                MessageBox.Show(
                    "Seleccione un festivo para eliminar.",
                    "Festivos",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBoxResult respuesta = MessageBox.Show(
                $"¿Desea eliminar el festivo del {FestivoSeleccionado.Fecha:dd/MM/yyyy}?",
                "Festivos",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (respuesta != MessageBoxResult.Yes)
            {
                return;
            }

            Festivo festivo = FestivoSeleccionado;

            try
            {
                FestivoService.Eliminar(festivo.Id);

                RegistrarAuditoriaFestivo(
                    accion: "Festivo.Eliminar",
                    ok: true,
                    usuarioAfectado: festivo.Nombre,
                    detalles: AuditoriaDetallesHelper.CrearJson(
                        ("FestivoId", festivo.Id),
                        ("Fecha", festivo.Fecha.ToString("yyyy-MM-dd")),
                        ("Tipo", festivo.Tipo)));

                LimpiarFormulario();
                CargarFestivos();
            }
            catch (Exception ex)
            {
                RegistrarAuditoriaFestivo(
                    accion: "Festivo.Eliminar",
                    ok: false,
                    usuarioAfectado: festivo.Nombre,
                    detalles: AuditoriaDetallesHelper.CrearJson(
                        ("FestivoId", festivo.Id),
                        ("Error", ex.Message)));

                MessageBox.Show(
                    $"No se pudo eliminar el festivo: {ex.Message}",
                    "Festivos",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LimpiarFormulario()
        {
            FestivoSeleccionado = null;
            NombreFestivo = string.Empty;
            TipoFestivo = "Nacional";
            FechaFestivo = DateTime.Today;
        }

        private static void RegistrarAuditoriaFestivo(string accion, bool ok, string usuarioAfectado, string detalles)
        {
            try
            {
                App.AuditoriaService?.RegistrarEvento(new AuditoriaEvento
                {
                    UsuarioAdmin = Sesion.UsuarioActual ?? "Sistema",
                    Accion = accion,
                    Modulo = "Festivos",
                    UsuarioAfectado = usuarioAfectado ?? string.Empty,
                    Resultado = ok,
                    FechaHora = DateTime.Now,
                    Tipo = "Festivo",
                    Detalles = detalles ?? string.Empty
                });
            }
            catch
            {
            }
        }
    }
}
