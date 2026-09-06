using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClinicaLongevidadApp.Converters
{
    [ValueConversion(typeof(DateTime), typeof(string))]
    public sealed class FechaCortaEspañolConverter : IValueConverter
    {
        private const string Formato = "dd/MM/yyyy HH:mm:ss";

        private static readonly CultureInfo CulturaEs = new("es-ES");

        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            return value is DateTime fecha
                ? fecha.ToString(Formato, CulturaEs)
                : string.Empty;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            if (value is string texto &&
                DateTime.TryParseExact(
                    texto.Trim(),
                    Formato,
                    CulturaEs,
                    DateTimeStyles.None,
                    out DateTime fecha))
            {
                return fecha;
            }

            return DependencyProperty.UnsetValue;
        }
    }
}