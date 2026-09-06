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
            }
            else
            {
                connection.Update(usuario);
            }
        }

        public static void Eliminar(int id)
        {
            using var connection = GetConnection();
            connection.Delete<Usuario>(id);
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

            string nuevaContraseña =
                PasswordGenerator.GenerarTemporal();

            usuario.PasswordHash =
                PasswordSecurity.HashPassword(nuevaContraseña);

            connection.Update(usuario);

            return nuevaContraseña;
        }
    }
}