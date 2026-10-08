using ClinicaLongevidadApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;

namespace ClinicaLongevidadApp.Services
{
    public static class FestivoService
    {
        private static readonly string DbPath =
            Path.Combine(ClinicaLongevidadApp.Services.AppPaths.BaseDir, "ClinicaLongevidad.db");

        private static SQLiteConnection GetConnection(string? databasePath = null)
        {
            var connection = new SQLiteConnection(databasePath ?? DbPath);
            connection.CreateTable<Festivo>();

            try
            {
                connection.Execute("ALTER TABLE Festivo ADD COLUMN Tipo TEXT NOT NULL DEFAULT 'Nacional'");
            }
            catch
            {
                // La columna ya existe.
            }

            return connection;
        }

        public static List<Festivo> ObtenerTodos()
        {
            using var connection = GetConnection();
            return connection.Table<Festivo>()
                .ToList()
                .Where(festivo => festivo.Activo)
                .OrderBy(festivo => festivo.Fecha)
                .ToList();
        }

        public static bool EsFestivo(DateTime fecha)
        {
            using var connection = GetConnection();
            return connection.Table<Festivo>()
                .ToList()
                .Any(festivo => festivo.Activo && festivo.Fecha.Date == fecha.Date);
        }

        public static void Guardar(Festivo festivo)
            => Guardar(festivo, App.AuditoriaService, DbPath);

        internal static void Guardar(Festivo festivo, AuditoriaService? auditoria, string databasePath)
        {
            ArgumentNullException.ThrowIfNull(festivo);
            RequireAuthorization();
            if (auditoria == null) throw new InvalidOperationException("No se puede guardar sin el servicio de auditoría.");
            using (var schema = GetConnection(databasePath)) { }

            festivo.Fecha = festivo.Fecha.Date;
            festivo.Tipo = string.IsNullOrWhiteSpace(festivo.Tipo) ? "Nacional" : festivo.Tipo.Trim();
            var originalId = festivo.Id;
            var savedId = originalId;

            auditoria.RegistrarEventoConOperacion(databasePath, connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = originalId == 0
                    ? "INSERT INTO Festivo (Fecha, Nombre, Tipo, Activo) VALUES (@fecha, @nombre, @tipo, @activo);"
                    : "UPDATE Festivo SET Fecha=@fecha, Nombre=@nombre, Tipo=@tipo, Activo=@activo WHERE Id=@id;";
                // sqlite-net stores DateTime as ticks by default. Preserve that format.
                command.Parameters.AddWithValue("@fecha", festivo.Fecha.Ticks);
                command.Parameters.AddWithValue("@nombre", (object?)festivo.Nombre ?? DBNull.Value);
                command.Parameters.AddWithValue("@tipo", festivo.Tipo);
                command.Parameters.AddWithValue("@activo", festivo.Activo ? 1 : 0);
                if (originalId != 0) command.Parameters.AddWithValue("@id", originalId);
                command.ExecuteNonQuery();
                if (originalId == 0)
                {
                    command.CommandText = "SELECT last_insert_rowid();";
                    savedId = checked(Convert.ToInt32(command.ExecuteScalar()));
                }
                return CreateEvent(originalId == 0 ? "Festivo.Crear" : "Festivo.Actualizar", festivo.Nombre,
                    AuditoriaDetallesHelper.CrearJson(("Id", savedId), ("Fecha", festivo.Fecha), ("Nombre", festivo.Nombre), ("Tipo", festivo.Tipo)));
            });
            // Do not expose an inserted ID until both records have committed.
            festivo.Id = savedId;
        }

        public static void Eliminar(int id)
            => Eliminar(id, App.AuditoriaService, DbPath);

        internal static void Eliminar(int id, AuditoriaService? auditoria, string databasePath)
        {
            RequireAuthorization();
            if (auditoria == null) throw new InvalidOperationException("No se puede eliminar sin el servicio de auditoría.");
            using (var schema = GetConnection(databasePath)) { }


            auditoria.RegistrarEventoConOperacion(databasePath, connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM Festivo WHERE Id=@id;";
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
                return CreateEvent("Festivo.Eliminar", id.ToString(), AuditoriaDetallesHelper.CrearJson(("Id", id)));
            });
        }

        private static void RequireAuthorization()
        {
            if (AuthorizationHelper.EnforcementEnabled() && !string.Equals(Sesion.RolActual ?? string.Empty, "Administración", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Operación no autorizada para el rol actual.");
        }

        private static AuditoriaEvento CreateEvent(string action, string? affectedUser, string details) => new AuditoriaEvento
        {
            UsuarioAdmin = Sesion.UsuarioActual ?? "Sistema",
            Accion = action,
            Modulo = "Festivos",
            UsuarioAfectado = affectedUser ?? string.Empty,
            Resultado = true,
            Detalles = details,
            FechaHora = DateTime.Now,
            Rol = Sesion.RolActual,
            Area = Sesion.AreaActual
        };
    }
}
