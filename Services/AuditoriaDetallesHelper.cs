using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClinicaLongevidadApp.Services
{
    public static class AuditoriaDetallesHelper
    {
        public static string CrearJson(params (string Clave, object? Valor)[] campos)
        {
            var datos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (clave, valor) in campos)
            {
                if (string.IsNullOrWhiteSpace(clave) || valor is null)
                {
                    continue;
                }

                datos[clave] = valor is DateTime fecha
                    ? fecha.ToString("O")
                    : valor.ToString() ?? string.Empty;
            }

            return JsonSerializer.Serialize(datos);
        }

        public static bool CoincideCampo(string? detalles, string clave, string valorEsperado)
        {
            if (!TryObtenerValor(detalles, clave, out string? valorReal))
            {
                return false;
            }

            // Perform a contains comparison (case-insensitive) to be compatible with
            // the helper used in UI/tests which supports "contains" semantics for filtering.
            return !string.IsNullOrWhiteSpace(valorReal) &&
                   !string.IsNullOrWhiteSpace(valorEsperado) &&
                   valorReal!.IndexOf(valorEsperado!.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool TryObtenerValor(string? detalles, string clave, out string valor)
        {
            valor = string.Empty;

            if (string.IsNullOrWhiteSpace(detalles) || string.IsNullOrWhiteSpace(clave))
            {
                return false;
            }

            string texto = detalles.Trim();

            if (texto.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(texto);
                    foreach (JsonProperty propiedad in doc.RootElement.EnumerateObject())
                    {
                        if (!string.Equals(propiedad.Name, clave, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        valor = propiedad.Value.ValueKind switch
                        {
                            JsonValueKind.String => propiedad.Value.GetString() ?? string.Empty,
                            JsonValueKind.Number => propiedad.Value.ToString(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            _ => propiedad.Value.ToString()
                        };

                        return true;
                    }
                }
                catch
                {
                    // Continuar con formato legado.
                }
            }

            // Formato legado: Clave=Valor;Clave2=Valor2
            foreach (string segmento in texto.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int indice = segmento.IndexOf('=');
                if (indice <= 0)
                {
                    continue;
                }

                string claveActual = segmento[..indice].Trim();
                if (!string.Equals(claveActual, clave, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                valor = segmento[(indice + 1)..].Trim();
                return true;
            }

            return false;
        }
    }
}
