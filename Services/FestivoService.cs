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
            using var connection = GetConnection();

            festivo.Fecha = festivo.Fecha.Date;
            festivo.Tipo = string.IsNullOrWhiteSpace(festivo.Tipo)
                ? "Nacional"
                : festivo.Tipo.Trim();

            if (festivo.Id == 0)
            {
                connection.Insert(festivo);
            }
            else
            {
                connection.Update(festivo);
            }
        }

        public static void Eliminar(int id)
        {
            using var connection = GetConnection();
            connection.Delete<Festivo>(id);
        }
    }
}
