using ClinicaLongevidadApp.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;

namespace ClinicaLongevidadApp.Services
{
    public static class PacienteService
    {
        private static readonly string dbPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClinicaLongevidad.db");

        private static SQLiteConnection GetConnection()
        {
            var conn = new SQLiteConnection(dbPath);
            conn.CreateTable<Paciente>();
            return conn;
        }

        public static List<Paciente> ObtenerTodos()
        {
            using var conn = GetConnection();
            return conn.Table<Paciente>().ToList();
        }

        public static void Guardar(Paciente paciente)
        {
            using var conn = GetConnection();

            if (paciente.Id == 0)
            {
                paciente.FechaCreacion = DateTime.Now;
                conn.Insert(paciente);
            }
            else
            {
                conn.Update(paciente);
            }
        }

        public static void Eliminar(int id)
        {
            using var conn = GetConnection();
            conn.Delete<Paciente>(id);
        }

        public static Paciente? ObtenerPorId(int id)
        {
            using var conn = GetConnection();
            return conn.Table<Paciente>().FirstOrDefault(p => p.Id == id);
        }
    }
}
