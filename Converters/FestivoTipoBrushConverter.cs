using ClinicaLongevidadApp.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace ClinicaLongevidadApp.Converters
{
    public sealed class FestivoTipoBrushConverter : IValueConverter
    {
        private static readonly Brush VerdeNacional = new SolidColorBrush(Color.FromRgb(152, 230, 155));
        private static readonly Brush LilaAutonomico = new SolidColorBrush(Color.FromRgb(201, 160, 255));
        private static readonly Brush NaranjaLocal = new SolidColorBrush(Color.FromRgb(255, 179, 107));
        private static readonly Brush BlancoNormal = Brushes.White;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!TryObtenerFecha(value, out DateTime fecha))
            {
                return BlancoNormal;
            }

            var festivo = FestivoService.ObtenerTodos()
                .FirstOrDefault(f => f.Fecha.Date == fecha.Date);

            if (festivo is null)
            {
                return BlancoNormal;
            }

            if (string.Equals(festivo.Tipo, "Local", StringComparison.OrdinalIgnoreCase))
            {
                return NaranjaLocal;
            }

            if (string.Equals(festivo.Tipo, "Autonomico", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(festivo.Tipo, "Autonómico", StringComparison.OrdinalIgnoreCase))
            {
                return LilaAutonomico;
            }

            return VerdeNacional;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static bool TryObtenerFecha(object value, out DateTime fecha)
        {
            if (value is DateTime fechaDirecta)
            {
                fecha = fechaDirecta.Date;
                return true;
            }

            if (value is not null)
            {
                var propiedadFecha = value.GetType().GetProperty("Date");
                if (propiedadFecha?.GetValue(value) is DateTime fechaInterna)
                {
                    fecha = fechaInterna.Date;
                    return true;
                }
            }

            fecha = default;
            return false;
        }
    }
}
