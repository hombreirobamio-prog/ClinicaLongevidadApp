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

        private static SQLiteConnection GetConnection(string? databasePath = null)
        {
            var connection = new SQLiteConnection(databasePath ?? DbPath);
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
            => Guardar(horario, App.AuditoriaService, DbPath);

        internal static void Guardar(HorarioProfesional horario, AuditoriaService? auditoria, string databasePath)
        {
            ArgumentNullException.ThrowIfNull(horario);
            if (auditoria == null) throw new InvalidOperationException("No se puede guardar sin el servicio de auditoría.");
            using (var schema = GetConnection(databasePath)) { }

            var originalId = horario.Id;
            var savedId = originalId;
            auditoria.RegistrarEventoConOperacion(databasePath, connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = originalId == 0
                    ? @"INSERT INTO HorarioProfesional (Profesional, DiaSemana, HoraInicio, HoraFin, IntervaloMinutos, Activo)
                        VALUES (@profesional, @dia, @inicio, @fin, @intervalo, @activo);"
                    : @"UPDATE HorarioProfesional SET Profesional=@profesional, DiaSemana=@dia, HoraInicio=@inicio,
                        HoraFin=@fin, IntervaloMinutos=@intervalo, Activo=@activo WHERE Id=@id;";
                void Add(string name, object? value) => command.Parameters.AddWithValue(name, value ?? DBNull.Value);
                Add("@profesional", horario.Profesional);
                Add("@dia", horario.DiaSemana);
                Add("@inicio", horario.HoraInicio);
                Add("@fin", horario.HoraFin);
                Add("@intervalo", horario.IntervaloMinutos);
                Add("@activo", horario.Activo ? 1 : 0);
                if (originalId != 0) Add("@id", originalId);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("El horario que se intenta guardar ya no existe.");
                if (originalId == 0)
                {
                    command.CommandText = "SELECT last_insert_rowid();";
                    savedId = checked(Convert.ToInt32(command.ExecuteScalar()));
                }
                return new AuditoriaEvento
                {
                    UsuarioAdmin = Sesion.UsuarioActual ?? "Sistema",
                    Accion = originalId == 0 ? "HorarioProfesional.Crear" : "HorarioProfesional.Actualizar",
                    Modulo = "Horarios",
                    UsuarioAfectado = horario.Profesional ?? string.Empty,
                    Resultado = true,
                    FechaHora = DateTime.Now,
                    Detalles = AuditoriaDetallesHelper.CrearJson(("Id", savedId), ("Profesional", horario.Profesional),
                        ("DiaSemana", horario.DiaSemana), ("HoraInicio", horario.HoraInicio), ("HoraFin", horario.HoraFin),
                        ("IntervaloMinutos", horario.IntervaloMinutos), ("Activo", horario.Activo)),
                    Rol = Sesion.RolActual,
                    Area = Sesion.AreaActual
                };
            });
            horario.Id = savedId;
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
