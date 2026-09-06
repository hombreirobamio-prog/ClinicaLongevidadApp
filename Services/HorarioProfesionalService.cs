using ClinicaLongevidadApp.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SQLite;

namespace ClinicaLongevidadApp.Services
{
    public static class HorarioProfesionalService
    {
        private static readonly string DbPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClinicaLongevidad.db");

        private static SQLiteConnection GetConnection()
        {
            var connection = new SQLiteConnection(DbPath);
            connection.CreateTable<HorarioProfesional>();
            return connection;
        }

        public static List<HorarioProfesional> Obtener(string profesional, DayOfWeek dia)
        {
            using var connection = GetConnection();
            return connection.Table<HorarioProfesional>()
                .ToList()
                .Where(horario => horario.Activo &&
                    string.Equals(horario.Profesional, profesional, StringComparison.OrdinalIgnoreCase) &&
                    horario.DiaSemana == (int)dia)
                .ToList();
        }

        public static List<string> ObtenerHorasDisponibles(
            string profesional,
            DateTime fecha,
            IEnumerable<Cita> citas)
        {
            if (string.IsNullOrWhiteSpace(profesional))
            {
                return new List<string>();
            }

            List<HorarioProfesional> horarios = Obtener(profesional, fecha.DayOfWeek);
            if (horarios.Count == 0)
            {
                horarios = ObtenerHorarioPredeterminado(profesional, fecha.DayOfWeek);
            }

            HashSet<string> horasOcupadas = citas
                .Where(cita => !string.Equals(cita.Estado, "Cancelada", StringComparison.OrdinalIgnoreCase))
                .Select(cita => cita.Hora)
                .ToHashSet(StringComparer.Ordinal);

            return horarios
                .SelectMany(GenerarHoras)
                .Where(hora => !horasOcupadas.Contains(hora))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(hora => hora)
                .ToList();
        }

        public static void Guardar(HorarioProfesional horario)
        {
            ArgumentNullException.ThrowIfNull(horario);
            using var connection = GetConnection();

            if (horario.Id == 0)
            {
                connection.Insert(horario);
            }
            else
            {
                connection.Update(horario);
            }
        }

        private static IEnumerable<string> GenerarHoras(HorarioProfesional horario)
        {
            if (!TimeSpan.TryParseExact(horario.HoraInicio, "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan inicio) ||
                !TimeSpan.TryParseExact(horario.HoraFin, "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan fin) ||
                horario.IntervaloMinutos <= 0)
            {
                return Enumerable.Empty<string>();
            }

            List<string> horas = new();
            for (TimeSpan hora = inicio; hora < fin; hora = hora.Add(TimeSpan.FromMinutes(horario.IntervaloMinutos)))
            {
                horas.Add(hora.ToString(@"hh\:mm", CultureInfo.InvariantCulture));
            }

            return horas;
        }

        private static List<HorarioProfesional> ObtenerHorarioPredeterminado(
            string profesional,
            DayOfWeek dia)
        {
            if (dia is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                return new List<HorarioProfesional>();
            }

            return new List<HorarioProfesional>
            {
                new()
                {
                    Profesional = profesional,
                    DiaSemana = (int)dia,
                    HoraInicio = "09:00",
                    HoraFin = "13:00",
                    IntervaloMinutos = 30
                },
                new()
                {
                    Profesional = profesional,
                    DiaSemana = (int)dia,
                    HoraInicio = "16:00",
                    HoraFin = "18:00",
                    IntervaloMinutos = 30
                }
            };
        }
    }
}
