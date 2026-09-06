using System;
using System.Globalization;
using System.Windows.Data;

namespace ClinicaLongevidadApp.Converters
{
    public class BoolToActivoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool activo)
                return activo ? "Activo" : "Inactivo";

            return "Desconocido";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
