using ClinicaLongevidadApp.Models;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace ClinicaLongevidadApp.Views
{
    public partial class PanelRecepcionView : UserControl
    {
        private readonly Dictionary<DateTime, string> _tiposFestivoPorFecha = new();
        private DateTime? _ultimaFechaValida;
        private bool _actualizandoFecha;
        private Calendar? _calendarioAbierto;

        public PanelRecepcionView() : this(new PanelRecepcionViewModel())
        {
        }

        public PanelRecepcionView(PanelRecepcionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            DatePickerFechaCita.CalendarOpened += DatePickerFechaCita_CalendarOpened;
            DatePickerFechaCita.CalendarClosed += DatePickerFechaCita_CalendarClosed;
            DatePickerFechaCita.SelectedDateChanged += DatePickerFechaCita_SelectedDateChanged;

            CargarFestivos();
            _ultimaFechaValida = DatePickerFechaCita.SelectedDate ?? DateTime.Today;
        }

        private void DatePickerFechaCita_CalendarOpened(object? sender, RoutedEventArgs e)
        {
            CargarFestivos();

            Calendar? calendario = ObtenerCalendario(DatePickerFechaCita);
            if (calendario is null)
            {
                return;
            }

            if (_calendarioAbierto is not null)
            {
                _calendarioAbierto.DisplayDateChanged -= Calendario_DisplayDateChanged;
                _calendarioAbierto.LayoutUpdated -= Calendario_LayoutUpdated;
            }

            _calendarioAbierto = calendario;
            _calendarioAbierto.DisplayDateChanged += Calendario_DisplayDateChanged;
            _calendarioAbierto.LayoutUpdated += Calendario_LayoutUpdated;

            DatePickerFechaCita.Dispatcher.BeginInvoke(
                PintarFestivosEnCalendario,
                DispatcherPriority.ApplicationIdle);
        }

        private void DatePickerFechaCita_CalendarClosed(object? sender, RoutedEventArgs e)
        {
            if (_calendarioAbierto is null)
            {
                return;
            }

            _calendarioAbierto.DisplayDateChanged -= Calendario_DisplayDateChanged;
            _calendarioAbierto.LayoutUpdated -= Calendario_LayoutUpdated;
            _calendarioAbierto = null;
        }

        private void Calendario_LayoutUpdated(object? sender, EventArgs e)
        {
            PintarFestivosEnCalendario();
        }

        private void Calendario_DisplayDateChanged(object? sender, CalendarDateChangedEventArgs e)
        {
            DatePickerFechaCita.Dispatcher.BeginInvoke(
                PintarFestivosEnCalendario,
                DispatcherPriority.ApplicationIdle);
        }

        private void DatePickerFechaCita_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_actualizandoFecha || DatePickerFechaCita.SelectedDate is null)
            {
                return;
            }

            DateTime fechaSeleccionada = DatePickerFechaCita.SelectedDate.Value.Date;
            if (_tiposFestivoPorFecha.ContainsKey(fechaSeleccionada))
            {
                _actualizandoFecha = true;
                MessageBox.Show(
                    "Ese día es festivo y no permite crear citas.",
                    "Crear cita",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                DatePickerFechaCita.SelectedDate = _ultimaFechaValida;
                _actualizandoFecha = false;
                return;
            }

            _ultimaFechaValida = fechaSeleccionada;
        }

        private void CargarFestivos()
        {
            _tiposFestivoPorFecha.Clear();

            foreach (Festivo festivo in FestivoService.ObtenerTodos())
            {
                _tiposFestivoPorFecha[festivo.Fecha.Date] = festivo.Tipo;
            }
        }

        private void PintarFestivosEnCalendario()
        {
            Calendar? calendario = ObtenerCalendario(DatePickerFechaCita);
            if (calendario is null)
            {
                return;
            }

            List<CalendarDayButton> botonesDia = FindVisualChildren<CalendarDayButton>(calendario)
                .Where(boton => int.TryParse(
                    ObtenerTextoBotonDia(boton),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _))
                .OrderBy(boton => boton.TranslatePoint(new Point(0, 0), calendario).Y)
                .ThenBy(boton => boton.TranslatePoint(new Point(0, 0), calendario).X)
                .ToList();

            if (botonesDia.Count == 0)
            {
                return;
            }

            DateTime primerDiaMes = new(calendario.DisplayDate.Year, calendario.DisplayDate.Month, 1);
            DayOfWeek primerDiaSemana = calendario.FirstDayOfWeek;
            int offset = ((int)primerDiaMes.DayOfWeek - (int)primerDiaSemana + 7) % 7;
            DateTime primeraFechaVisible = primerDiaMes.AddDays(-offset);

            for (int i = 0; i < botonesDia.Count; i++)
            {
                CalendarDayButton botonDia = botonesDia[i];
                DateTime fecha = primeraFechaVisible.AddDays(i).Date;
                bool esHoy = EsBotonDiaHoy(botonDia, fecha);

                if (_tiposFestivoPorFecha.TryGetValue(fecha, out string? tipo))
                {
                    AplicarEstiloFestivo(botonDia, ObtenerColorPorTipo(tipo), tipo);
                    if (esHoy)
                    {
                        AplicarResaltadoHoy(botonDia);
                    }
                }
                else if (esHoy)
                {
                    LimpiarEstiloFestivo(botonDia);
                    AplicarResaltadoHoy(botonDia);
                }
                else
                {
                    LimpiarEstiloFestivo(botonDia);
                }
            }
        }

        private static bool EsBotonDiaHoy(CalendarDayButton botonDia, DateTime fechaCalculada)
        {
            var propiedadIsToday = botonDia.GetType().GetProperty("IsToday");
            if (propiedadIsToday?.GetValue(botonDia) is bool isToday)
            {
                return isToday;
            }

            return fechaCalculada.Date == DateTime.Today;
        }

        private static void AplicarResaltadoHoy(CalendarDayButton botonDia)
        {
            botonDia.Foreground = Brushes.Black;
            botonDia.FontWeight = FontWeights.Bold;
            botonDia.BorderBrush = Brushes.Black;
            botonDia.BorderThickness = new Thickness(2);

            Border? bordeInterno = FindVisualChild<Border>(botonDia);
            if (bordeInterno is not null)
            {
                bordeInterno.BorderBrush = Brushes.Black;
                bordeInterno.BorderThickness = new Thickness(2);
            }
        }

        private static string ObtenerTextoBotonDia(CalendarDayButton botonDia)
        {
            if (!string.IsNullOrWhiteSpace(botonDia.Content?.ToString()))
            {
                return botonDia.Content.ToString()!;
            }

            TextBlock? texto = FindVisualChild<TextBlock>(botonDia);
            return texto?.Text ?? string.Empty;
        }

        private static void AplicarEstiloFestivo(
            CalendarDayButton botonDia,
            Brush color,
            string? tipo)
        {
            botonDia.Background = color;
            botonDia.Foreground = Brushes.Black;
            botonDia.BorderBrush = Brushes.Transparent;
            botonDia.Opacity = 1;
            botonDia.FontWeight = FontWeights.Bold;
            botonDia.ToolTip = $"Festivo {tipo}";

            Border? bordeInterno = FindVisualChild<Border>(botonDia);
            if (bordeInterno is not null)
            {
                bordeInterno.Background = color;
                bordeInterno.BorderBrush = Brushes.Transparent;
            }
        }

        private static void LimpiarEstiloFestivo(CalendarDayButton botonDia)
        {
            botonDia.ClearValue(BackgroundProperty);
            botonDia.ClearValue(ForegroundProperty);
            botonDia.ClearValue(BorderBrushProperty);
            botonDia.ClearValue(OpacityProperty);
            botonDia.ClearValue(FontWeightProperty);
            botonDia.ClearValue(ToolTipProperty);
            botonDia.ClearValue(BorderThicknessProperty);

            Border? bordeInterno = FindVisualChild<Border>(botonDia);
            if (bordeInterno is not null)
            {
                bordeInterno.ClearValue(Border.BackgroundProperty);
                bordeInterno.ClearValue(Border.BorderBrushProperty);
                bordeInterno.ClearValue(Border.BorderThicknessProperty);
            }
        }

        private static bool TryObtenerFechaDesdeBoton(
            CalendarDayButton botonDia,
            Calendar calendario,
            out DateTime fecha)
        {
            if (botonDia.DataContext is DateTime fechaDirecta)
            {
                fecha = fechaDirecta.Date;
                return true;
            }

            object? contexto = botonDia.DataContext;
            if (contexto is not null)
            {
                var propiedadFecha = contexto.GetType().GetProperty("Date");
                object? valorFecha = propiedadFecha?.GetValue(contexto);
                if (valorFecha is DateTime fechaInterna)
                {
                    fecha = fechaInterna.Date;
                    return true;
                }
            }

            if (int.TryParse(ObtenerTextoBotonDia(botonDia), out int dia) && dia >= 1 && dia <= 31)
            {
                bool esInactivo = false;
                var propiedadInactivo = botonDia.GetType().GetProperty("IsInactive");
                if (propiedadInactivo?.GetValue(botonDia) is bool valorInactivo)
                {
                    esInactivo = valorInactivo;
                }

                DateTime mesVisible = new(calendario.DisplayDate.Year, calendario.DisplayDate.Month, 1);
                if (!esInactivo)
                {
                    fecha = new DateTime(mesVisible.Year, mesVisible.Month, dia);
                    return true;
                }

                DateTime mesAnterior = mesVisible.AddMonths(-1);
                DateTime mesSiguiente = mesVisible.AddMonths(1);

                fecha = dia > 20
                    ? new DateTime(mesAnterior.Year, mesAnterior.Month, dia)
                    : new DateTime(mesSiguiente.Year, mesSiguiente.Month, dia);
                return true;
            }

            fecha = default;
            return false;
        }

        private static Calendar? ObtenerCalendario(DatePicker datePicker)
        {
            datePicker.ApplyTemplate();

            if (datePicker.Template.FindName("PART_Popup", datePicker) is not Popup popup ||
                popup.Child is null)
            {
                return null;
            }

            return FindVisualChild<Calendar>(popup.Child);
        }

        private static Brush ObtenerColorPorTipo(string? tipo)
        {
            if (string.Equals(tipo, "Local", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Color.FromRgb(255, 179, 107));
            }

            if (string.Equals(tipo, "Autonomico", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tipo, "Autonómico", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Color.FromRgb(201, 160, 255));
            }

            return new SolidColorBrush(Color.FromRgb(152, 230, 155));
        }

        private static T? FindVisualChild<T>(DependencyObject? parent)
            where T : DependencyObject
        {
            if (parent is null)
            {
                return null;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                T? descendant = FindVisualChild<T>(child);
                if (descendant is not null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent is null)
            {
                yield break;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (T descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}