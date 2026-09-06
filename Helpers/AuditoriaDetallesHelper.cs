using System;
using System.Text.Json;

namespace ClinicaLongevidadApp.Helpers
{
    public static class AuditoriaDetallesHelper
    {
        /// <summary>
        /// Comprueba si en el campo indicado dentro de <paramref name="detalles"/>
        /// existe (o contiene) el <paramref name="valor"/> buscado.
        /// Soporta JSON ({ ... }) y el formato legado "Clave=Valor;Clave2=Valor2".
        /// Comparaciones case-insensitive.
        /// </summary>
        public static bool CoincideCampo(string? detalles, string campo, string valor)
        {
            if (string.IsNullOrWhiteSpace(detalles) ||
                string.IsNullOrWhiteSpace(campo) ||
                string.IsNullOrWhiteSpace(valor))
            {
                return false;
            }

            string texto = detalles.Trim();
            try
            {
                if (texto.StartsWith("{", StringComparison.Ordinal))
                {
                    using var doc = JsonDocument.Parse(texto);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (!string.Equals(prop.Name, campo, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Only consider primitive values (string/number/bool/null).
                        // If the JSON property is an object/array, treat as non-matching.
                        switch (prop.Value.ValueKind)
                        {
                            case System.Text.Json.JsonValueKind.String:
                                {
                                    var propVal = prop.Value.GetString() ?? string.Empty;
                                    return propVal.Contains(valor, StringComparison.OrdinalIgnoreCase);
                                }
                            case System.Text.Json.JsonValueKind.Number:
                            case System.Text.Json.JsonValueKind.True:
                            case System.Text.Json.JsonValueKind.False:
                            case System.Text.Json.JsonValueKind.Null:
                                {
                                    var propVal = prop.Value.GetRawText() ?? string.Empty;
                                    return propVal.Contains(valor, StringComparison.OrdinalIgnoreCase);
                                }
                            default:
                                return false;
                        }
                    }

                    return false;
                }
            }
            catch
            {
                // Si el parseo JSON falla, continuamos tratando formato legado.
            }

            // Formato legado: "Clave=Valor;Otro=Algo"
            try
            {
                var pairs = texto.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var idx = pair.IndexOf('=');
                    if (idx <= 0) continue;
                    var key = pair.Substring(0, idx).Trim();
                    var val = pair.Substring(idx + 1).Trim();
                    if (string.Equals(key, campo, StringComparison.OrdinalIgnoreCase))
                    {
                        if (val.Contains(valor, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch
            {
                // Ignorar y devolver false si hay cualquier error en el parseo legado.
            }

            return false;
        }
    }
}