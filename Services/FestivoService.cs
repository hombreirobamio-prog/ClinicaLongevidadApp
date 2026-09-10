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
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClinicaLongevidad.db");

        private static SQLiteConnection GetConnection()
        {
            var connection = new SQLiteConnection(DbPath);
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
        {
            ArgumentNullException.ThrowIfNull(festivo);
            // Authorization: require Administración role when enforcement is enabled
            if (AuthorizationHelper.EnforcementEnabled() && !string.Equals(Sesion.RolActual ?? string.Empty, "Administración", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Operación no autorizada para el rol actual.");
            }
            using var connection = GetConnection();

            festivo.Fecha = festivo.Fecha.Date;
            festivo.Tipo = string.IsNullOrWhiteSpace(festivo.Tipo)
                ? "Nacional"
                : festivo.Tipo.Trim();

            if (festivo.Id == 0)
            {
                connection.Insert(festivo);
                try
                {
                    App.AuditoriaService?.RegistrarEvento(new Models.AuditoriaEvento
                    {
                        UsuarioAdmin = Services.Sesion.UsuarioActual ?? "Sistema",
                        Accion = "Festivo.Crear",
                        Modulo = "Festivos",
                        UsuarioAfectado = festivo.Nombre ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.Now,
                        Detalles = AuditoriaDetallesHelper.CrearJson(("Id", festivo.Id), ("Fecha", festivo.Fecha), ("Nombre", festivo.Nombre), ("Tipo", festivo.Tipo)),
                        Rol = Services.Sesion.RolActual,
                        Area = Services.Sesion.AreaActual
                    });
                }
                catch { }
            }
            else
            {
                connection.Update(festivo);
                try
                {
                    App.AuditoriaService?.RegistrarEvento(new Models.AuditoriaEvento
                    {
                        UsuarioAdmin = Services.Sesion.UsuarioActual ?? "Sistema",
                        Accion = "Festivo.Actualizar",
                        Modulo = "Festivos",
                        UsuarioAfectado = festivo.Nombre ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.Now,
                        Detalles = AuditoriaDetallesHelper.CrearJson(("Id", festivo.Id), ("Fecha", festivo.Fecha), ("Nombre", festivo.Nombre), ("Tipo", festivo.Tipo)),
                        Rol = Services.Sesion.RolActual,
                        Area = Services.Sesion.AreaActual
                    });
                }
                catch { }
            }
        }

        public static void Eliminar(int id)
        {
            // Authorization: require Administración role when enforcement is enabled
            if (AuthorizationHelper.EnforcementEnabled() && !string.Equals(Sesion.RolActual ?? string.Empty, "Administración", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Operación no autorizada para el rol actual.");
            }

            using var connection = GetConnection();
            connection.Delete<Festivo>(id);
            try
            {
                App.AuditoriaService?.RegistrarEvento(new Models.AuditoriaEvento
                {
                    UsuarioAdmin = Services.Sesion.UsuarioActual ?? "Sistema",
                    Accion = "Festivo.Eliminar",
                    Modulo = "Festivos",
                    UsuarioAfectado = id.ToString(),
                    Resultado = true,
                    FechaHora = DateTime.Now,
                    Detalles = AuditoriaDetallesHelper.CrearJson(("Id", id)),
                    Rol = Services.Sesion.RolActual,
                    Area = Services.Sesion.AreaActual
                });
            }
            catch { }
        }
    }
}
