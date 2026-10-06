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
            System.IO.Path.Combine(ClinicaLongevidadApp.Services.AppPaths.BaseDir, "ClinicaLongevidad.db");

        private static SQLiteConnection GetConnection(string? databasePath = null)
        {
            var connection = new SQLiteConnection(databasePath ?? DbPath);
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
            => Guardar(cita, App.AuditoriaService, DbPath);

        internal static void Guardar(Cita cita, AuditoriaService? auditoria, string databasePath)
        {
            ArgumentNullException.ThrowIfNull(cita);
            AuthorizationHelper.EnsureRole("Recepcion", "Administración");
            if (auditoria == null) throw new InvalidOperationException("No se puede guardar sin el servicio de auditoría.");
            using (var schema = GetConnection(databasePath)) { }

            var originalId = cita.Id;
            var savedId = originalId;
            var createdAt = originalId == 0 ? DateTime.Now : cita.FechaCreacion;
            auditoria.RegistrarEventoConOperacion(databasePath, connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = originalId == 0
                    ? @"INSERT INTO Cita (PacienteId, PacienteNombre, Fecha, Hora, Profesional, Estado, FechaCreacion)
                        VALUES (@pacienteId, @nombre, @fecha, @hora, @profesional, @estado, @creacion);"
                    : @"UPDATE Cita SET PacienteId=@pacienteId, PacienteNombre=@nombre, Fecha=@fecha, Hora=@hora,
                        Profesional=@profesional, Estado=@estado, FechaCreacion=@creacion WHERE Id=@id;";
                void Add(string name, object? value) => command.Parameters.AddWithValue(name, value ?? DBNull.Value);
                Add("@pacienteId", cita.PacienteId);
                Add("@nombre", cita.PacienteNombre);
                // Preserve sqlite-net's DateTime ticks representation.
                Add("@fecha", cita.Fecha.Ticks);
                Add("@hora", cita.Hora);
                Add("@profesional", cita.Profesional);
                Add("@estado", cita.Estado);
                Add("@creacion", createdAt.Ticks);
                if (originalId != 0) Add("@id", originalId);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("La cita que se intenta guardar ya no existe.");
                if (originalId == 0)
                {
                    command.CommandText = "SELECT last_insert_rowid();";
                    savedId = checked(Convert.ToInt32(command.ExecuteScalar()));
                }
                return new AuditoriaEvento
                {
                    UsuarioAdmin = Sesion.UsuarioActual ?? "Sistema",
                    Accion = originalId == 0 ? "Cita.Crear" : "Cita.Actualizar",
                    Modulo = "Citas",
                    UsuarioAfectado = cita.PacienteNombre ?? string.Empty,
                    Resultado = true,
                    FechaHora = DateTime.Now,
                    Detalles = AuditoriaDetallesHelper.CrearJson(("Id", savedId), ("PacienteId", cita.PacienteId), ("PacienteNombre", cita.PacienteNombre), ("Fecha", cita.Fecha), ("Hora", cita.Hora), ("Profesional", cita.Profesional), ("Estado", cita.Estado)),
                    Rol = Sesion.RolActual,
                    Area = Sesion.AreaActual
                };
            });
            cita.Id = savedId;
            cita.FechaCreacion = createdAt;
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
