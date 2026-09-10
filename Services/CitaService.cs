using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClinicaLongevidadApp.Models;
using SQLite;

namespace ClinicaLongevidadApp.Services
{
    public static class CitaService
    {
        private static readonly string DbPath =
            System.IO.Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "ClinicaLongevidad.db");

        private static SQLiteConnection GetConnection()
        {
            var connection = new SQLiteConnection(DbPath);
            connection.CreateTable<Cita>();
            return connection;
        }

        public static List<Cita> ObtenerPorFecha(DateTime fecha)
        {
            using var connection = GetConnection();

            return connection.Table<Cita>()
                .ToList()
                .Where(cita => cita.Fecha.Date == fecha.Date)
                .OrderBy(cita => cita.Hora)
                .ThenBy(cita => cita.Profesional)
                .ToList();
        }

        public static bool EstaOcupada(
            DateTime fecha,
            string hora,
            string profesional,
            int citaId = 0)
        {
            using var connection = GetConnection();

            return connection.Table<Cita>()
                .ToList()
                .Any(cita => cita.Id != citaId &&
                    cita.Fecha.Date == fecha.Date &&
                    string.Equals(cita.Hora, hora, StringComparison.Ordinal) &&
                    string.Equals(
                        cita.Profesional,
                        profesional,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        cita.Estado,
                        "Cancelada",
                        StringComparison.OrdinalIgnoreCase));
        }

        public static void Guardar(Cita cita)
        {
            ArgumentNullException.ThrowIfNull(cita);
            // Authorization: require Recepcion or Administración role when enforcement is enabled
            try { AuthorizationHelper.EnsureRole("Recepcion", "Administración"); } catch { throw; }

            using var connection = GetConnection();

            if (cita.Id == 0)
            {
                cita.FechaCreacion = DateTime.Now;
                connection.Insert(cita);
                // Auditar creación de cita
                try
                {
                    App.AuditoriaService?.RegistrarEvento(new Models.AuditoriaEvento
                    {
                        UsuarioAdmin = Services.Sesion.UsuarioActual ?? "Sistema",
                        Accion = "Cita.Crear",
                        Modulo = "Citas",
                        UsuarioAfectado = cita.PacienteNombre ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.Now,
                        Detalles = AuditoriaDetallesHelper.CrearJson(("Id", cita.Id), ("PacienteId", cita.PacienteId), ("PacienteNombre", cita.PacienteNombre), ("Fecha", cita.Fecha), ("Hora", cita.Hora), ("Profesional", cita.Profesional), ("Estado", cita.Estado)),
                        Rol = Services.Sesion.RolActual,
                        Area = Services.Sesion.AreaActual
                    });
                }
                catch { }
            }
            else
            {
                connection.Update(cita);
                // Auditar actualización de cita
                try
                {
                    App.AuditoriaService?.RegistrarEvento(new Models.AuditoriaEvento
                    {
                        UsuarioAdmin = Services.Sesion.UsuarioActual ?? "Sistema",
                        Accion = "Cita.Actualizar",
                        Modulo = "Citas",
                        UsuarioAfectado = cita.PacienteNombre ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.Now,
                        Detalles = AuditoriaDetallesHelper.CrearJson(("Id", cita.Id), ("PacienteId", cita.PacienteId), ("PacienteNombre", cita.PacienteNombre), ("Fecha", cita.Fecha), ("Hora", cita.Hora), ("Profesional", cita.Profesional), ("Estado", cita.Estado)),
                        Rol = Services.Sesion.RolActual,
                        Area = Services.Sesion.AreaActual
                    });
                }
                catch { }
            }
        }

        public static List<Cita> ObtenerPorPaciente(int pacienteId)
        {
            using var connection = GetConnection();

            return connection.Table<Cita>()
                .ToList()
                .Where(cita => cita.PacienteId == pacienteId)
                .OrderBy(cita => cita.Fecha.Date)
                .ThenBy(cita => ParseHora(cita.Hora))
                .ToList();
        }

        private static TimeSpan ParseHora(string? hora)
        {
            return TimeSpan.TryParseExact(
                hora ?? string.Empty,
                "hh\\:mm",
                CultureInfo.InvariantCulture,
                out TimeSpan valor)
                ? valor
                : TimeSpan.Zero;
        }
    }
}
