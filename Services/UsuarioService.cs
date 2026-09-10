using ClinicaLongevidadApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;

namespace ClinicaLongevidadApp.Services
{
    public static class UsuarioService
    {
        private static readonly string DbPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "ClinicaLongevidad.db");

        private static SQLiteConnection GetConnection()
        {
            var connection = new SQLiteConnection(DbPath);
            connection.CreateTable<Usuario>();
            return connection;
        }

        public static List<Usuario> ObtenerTodos()
        {
            using var connection = GetConnection();
            return connection.Table<Usuario>().ToList();
        }

        public static void Guardar(Usuario usuario)
        {
            ArgumentNullException.ThrowIfNull(usuario);
            // Authorization: require Administración role when enforcement is enabled
            try { AuthorizationHelper.EnsureRole("Administración"); } catch { throw; }

            using var connection = GetConnection();

            // Solo se genera un hash nuevo si se ha introducido
            // una contraseña en texto plano.
            if (!string.IsNullOrWhiteSpace(usuario.PasswordHash) &&
                !usuario.PasswordHash.StartsWith(
                    "PBKDF2$",
                    StringComparison.Ordinal))
            {
                usuario.PasswordHash =
                    PasswordSecurity.HashPassword(usuario.PasswordHash);
            }

            if (usuario.Id == 0)
            {
                usuario.FechaCreacion = DateTime.Now;
                connection.Insert(usuario);
                // Auditar creación de usuario (no incluir PasswordHash)
                try
                {
                    App.AuditoriaService?.RegistrarEvento(new Models.AuditoriaEvento
                    {
                        UsuarioAdmin = Services.Sesion.UsuarioActual ?? "Sistema",
                        Accion = "Usuario.Crear",
                        Modulo = "Usuarios",
                        UsuarioAfectado = usuario.NombreUsuario ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.Now,
                        Detalles = AuditoriaDetallesHelper.CrearJson(("Id", usuario.Id), ("NombreCompleto", usuario.NombreCompleto), ("Email", usuario.Email), ("Activo", usuario.Activo), ("Rol", usuario.Rol), ("Area", usuario.Area)),
                        Rol = Services.Sesion.RolActual,
                        Area = Services.Sesion.AreaActual
                    });
                }
                catch { }
            }
            else
            {
                connection.Update(usuario);
                // Auditar actualización de usuario (no incluir PasswordHash)
                try
                {
                    App.AuditoriaService?.RegistrarEvento(new Models.AuditoriaEvento
                    {
                        UsuarioAdmin = Services.Sesion.UsuarioActual ?? "Sistema",
                        Accion = "Usuario.Actualizar",
                        Modulo = "Usuarios",
                        UsuarioAfectado = usuario.NombreUsuario ?? string.Empty,
                        Resultado = true,
                        FechaHora = DateTime.Now,
                        Detalles = AuditoriaDetallesHelper.CrearJson(("Id", usuario.Id), ("NombreCompleto", usuario.NombreCompleto), ("Email", usuario.Email), ("Activo", usuario.Activo), ("Rol", usuario.Rol), ("Area", usuario.Area)),
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
            AuthorizationHelper.EnsureRole("Administración");
            using var connection = GetConnection();
            connection.Delete<Usuario>(id);
            try
            {
                App.AuditoriaService?.RegistrarEvento(new Models.AuditoriaEvento
                {
                    UsuarioAdmin = Services.Sesion.UsuarioActual ?? "Sistema",
                    Accion = "Usuario.Eliminar",
                    Modulo = "Usuarios",
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

        public static Usuario? ObtenerPorNombre(string nombreUsuario)
        {
            string nombreNormalizado = nombreUsuario?.Trim() ?? string.Empty;

            if (nombreNormalizado.Length == 0)
            {
                return null;
            }

            using var connection = GetConnection();

            return connection.Table<Usuario>().ToList()
                .FirstOrDefault(usuario =>
                    string.Equals(
                        usuario.NombreUsuario?.Trim(),
                        nombreNormalizado,
                        StringComparison.OrdinalIgnoreCase));
        }

        public static bool ValidarLogin(
            string usuario,
            string contraseña)
        {
            var usuarioEncontrado = ObtenerPorNombre(usuario);

            if (usuarioEncontrado is null ||
                !usuarioEncontrado.Activo)
            {
                // Registrar intento de login fallido
                try
                {
                    App.AuditoriaService?.RegistrarEvento(new Models.AuditoriaEvento
                    {
                        UsuarioAdmin = Services.Sesion.UsuarioActual ?? "Sistema",
                        Accion = "Usuario.Login",
                        Modulo = "Usuarios",
                        UsuarioAfectado = usuario ?? string.Empty,
                        Resultado = false,
                        FechaHora = DateTime.Now,
                        Detalles = AuditoriaDetallesHelper.CrearJson(("Razon", usuarioEncontrado is null ? "NoEncontrado" : "Inactivo")),
                        Rol = Services.Sesion.RolActual,
                        Area = Services.Sesion.AreaActual
                    });
                }
                catch { }

                return false;
            }

            return PasswordSecurity.VerifyPassword(
                contraseña,
                usuarioEncontrado.PasswordHash);
        }

        public static string RestablecerContraseña(int usuarioId)
        {
            using var connection = GetConnection();

            var usuario = connection.Table<Usuario>()
                .FirstOrDefault(item => item.Id == usuarioId)
                ?? throw new InvalidOperationException(
                    "Usuario no encontrado.");

            // Authorization: allow admin or the user themselves to reset password when enforcement is enabled
            AuthorizationHelper.EnsureAdminOrSelf(usuario.NombreUsuario ?? string.Empty);

            string nuevaContraseña =
                PasswordGenerator.GenerarTemporal();

            usuario.PasswordHash =
                PasswordSecurity.HashPassword(nuevaContraseña);

            connection.Update(usuario);

            try
            {
                App.AuditoriaService?.RegistrarEvento(new Models.AuditoriaEvento
                {
                    UsuarioAdmin = Services.Sesion.UsuarioActual ?? "Sistema",
                    Accion = "Usuario.RestablecerContraseña",
                    Modulo = "Usuarios",
                    UsuarioAfectado = usuario.NombreUsuario ?? string.Empty,
                    Resultado = true,
                    FechaHora = DateTime.Now,
                    Detalles = AuditoriaDetallesHelper.CrearJson(("Id", usuario.Id), ("Nota", "Contraseña temporal generada (no registrada por seguridad)")),
                    Rol = Services.Sesion.RolActual,
                    Area = Services.Sesion.AreaActual
                });
            }
            catch { }

            return nuevaContraseña;
        }
    }
}
