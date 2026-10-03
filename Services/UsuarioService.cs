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

        private static SQLiteConnection GetConnection(string? databasePath = null)
        {
            var connection = new SQLiteConnection(databasePath ?? DbPath);
            connection.CreateTable<Usuario>();
            return connection;
        }

        public static List<Usuario> ObtenerTodos()
        {
            using var connection = GetConnection();
            return connection.Table<Usuario>().ToList();
        }

        public static void Guardar(Usuario usuario)
            => Guardar(usuario, App.AuditoriaService, DbPath);

        internal static void Guardar(Usuario usuario, AuditoriaService? auditoria, string databasePath)
        {
            ArgumentNullException.ThrowIfNull(usuario);
            AuthorizationHelper.EnsureRole("Administración");
            if (auditoria == null) throw new InvalidOperationException("No se puede guardar sin el servicio de auditoría.");
            using (var schema = GetConnection(databasePath)) { }

            var originalId = usuario.Id;
            var savedId = originalId;
            var createdAt = originalId == 0 ? DateTime.Now : usuario.FechaCreacion;
            var passwordHash = usuario.PasswordHash;
            if (!string.IsNullOrWhiteSpace(passwordHash) && !passwordHash.StartsWith("PBKDF2$", StringComparison.Ordinal))
                passwordHash = PasswordSecurity.HashPassword(passwordHash);

            auditoria.RegistrarEventoConOperacion(databasePath, connection =>
            {
                string? previousRole = null, previousArea = null;
                bool? previousActive = null;
                bool passwordChanged = false;
                using var command = connection.CreateCommand();
                if (originalId != 0)
                {
                    command.CommandText = "SELECT Rol, Area, Activo, PasswordHash FROM Usuario WHERE Id=@id;";
                    command.Parameters.AddWithValue("@id", originalId);
                    using var reader = command.ExecuteReader();
                    if (!reader.Read()) throw new InvalidOperationException("Usuario no encontrado.");
                    previousRole = reader.IsDBNull(0) ? null : reader.GetString(0);
                    previousArea = reader.IsDBNull(1) ? null : reader.GetString(1);
                    previousActive = reader.GetBoolean(2);
                    passwordChanged = !string.Equals(reader.IsDBNull(3) ? null : reader.GetString(3), passwordHash, StringComparison.Ordinal);
                }
                command.CommandText = originalId == 0
                    ? @"INSERT INTO Usuario (NombreUsuario, NombreCompleto, Email, TelefonoInterno, PasswordHash, Activo, Rol, Area, FechaCreacion, UltimoAcceso)
                        VALUES (@nombre, @completo, @email, @telefono, @password, @activo, @rol, @area, @creacion, @acceso);"
                    : @"UPDATE Usuario SET NombreUsuario=@nombre, NombreCompleto=@completo, Email=@email, TelefonoInterno=@telefono,
                        PasswordHash=@password, Activo=@activo, Rol=@rol, Area=@area, FechaCreacion=@creacion, UltimoAcceso=@acceso WHERE Id=@id;";
                void Add(string name, object? value) => command.Parameters.AddWithValue(name, value ?? DBNull.Value);
                Add("@nombre", usuario.NombreUsuario);
                Add("@completo", usuario.NombreCompleto);
                Add("@email", usuario.Email);
                Add("@telefono", usuario.TelefonoInterno);
                Add("@password", passwordHash);
                Add("@activo", usuario.Activo);
                Add("@rol", usuario.Rol);
                Add("@area", usuario.Area);
                // Keep sqlite-net's ticks representation for both dates.
                Add("@creacion", createdAt.Ticks);
                Add("@acceso", usuario.UltimoAcceso?.Ticks);
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Usuario no encontrado.");
                if (originalId == 0)
                {
                    command.CommandText = "SELECT last_insert_rowid();";
                    savedId = checked(Convert.ToInt32(command.ExecuteScalar()));
                }
                return CreateEvent(originalId == 0 ? "Usuario.Crear" : "Usuario.Actualizar", usuario.NombreUsuario,
                    AuditoriaDetallesHelper.CrearJson(("Id", savedId), ("NombreCompleto", usuario.NombreCompleto), ("Email", usuario.Email),
                        ("Activo", usuario.Activo), ("Rol", usuario.Rol), ("Area", usuario.Area),
                        ("RolAnterior", previousRole), ("AreaAnterior", previousArea), ("ActivoAnterior", previousActive),
                        ("PasswordCambiada", passwordChanged)));
            });
            usuario.Id = savedId;
            usuario.FechaCreacion = createdAt;
            usuario.PasswordHash = passwordHash;
        }

        public static void Eliminar(int id)
            => Eliminar(id, App.AuditoriaService, DbPath);

        internal static void Eliminar(int id, AuditoriaService? auditoria, string databasePath)
        {
            AuthorizationHelper.EnsureRole("Administración");
            if (auditoria == null) throw new InvalidOperationException("No se puede eliminar sin el servicio de auditoría.");
            using (var schema = GetConnection(databasePath)) { }
            auditoria.RegistrarEventoConOperacion(databasePath, connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT NombreUsuario FROM Usuario WHERE Id=@id;";
                command.Parameters.AddWithValue("@id", id);
                var name = command.ExecuteScalar() as string;
                command.CommandText = "DELETE FROM Usuario WHERE Id=@id;";
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Usuario no encontrado.");
                return CreateEvent("Usuario.Eliminar", id.ToString(),
                    AuditoriaDetallesHelper.CrearJson(("Id", id), ("NombreUsuario", name)));
            });
        }

        private static AuditoriaEvento CreateEvent(string action, string? affectedUser, string details) => new()
        {
            UsuarioAdmin = Sesion.UsuarioActual ?? "Sistema",
            Accion = action,
            Modulo = "Usuarios",
            UsuarioAfectado = affectedUser ?? string.Empty,
            Resultado = true,
            FechaHora = DateTime.Now,
            Detalles = details,
            Rol = Sesion.RolActual,
            Area = Sesion.AreaActual
        };

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
            => RestablecerContraseña(usuarioId, App.AuditoriaService, DbPath);

        internal static string RestablecerContraseña(int usuarioId, AuditoriaService? auditoria, string databasePath)
        {
            if (auditoria == null) throw new InvalidOperationException("No se puede restablecer la contraseña sin el servicio de auditoría.");
            using (var schema = GetConnection(databasePath)) { }
            string nuevaContraseña = string.Empty;
            auditoria.RegistrarEventoConOperacion(databasePath, connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT NombreUsuario FROM Usuario WHERE Id=@id;";
                command.Parameters.AddWithValue("@id", usuarioId);
                var name = command.ExecuteScalar();
                if (name == null) throw new InvalidOperationException("Usuario no encontrado.");
                var userName = name as string ?? string.Empty;
                AuthorizationHelper.EnsureAdminOrSelf(userName);
                nuevaContraseña = PasswordGenerator.GenerarTemporal();
                command.CommandText = "UPDATE Usuario SET PasswordHash=@password WHERE Id=@id;";
                command.Parameters.AddWithValue("@password", PasswordSecurity.HashPassword(nuevaContraseña));
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Usuario no encontrado.");
                return CreateEvent("Usuario.RestablecerContraseña", userName,
                    AuditoriaDetallesHelper.CrearJson(("Id", usuarioId), ("Nota", "Contraseña temporal generada (no registrada por seguridad)")));
            });
            return nuevaContraseña;
        }
    }
}
