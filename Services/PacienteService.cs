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
            Path.Combine(ClinicaLongevidadApp.Services.AppPaths.BaseDir,
            "ClinicaLongevidad.db");

        private static SQLiteConnection GetConnection(string? databasePath = null)
        {
            var conn = new SQLiteConnection(databasePath ?? dbPath);
            conn.CreateTable<Paciente>();
            return conn;
        }

        public static List<Paciente> ObtenerTodos()
        {
            using var conn = GetConnection();
            return conn.Table<Paciente>().ToList();
        }

        public static void Guardar(Paciente paciente)
            => Guardar(paciente, App.AuditoriaService, dbPath);

        internal static void Guardar(Paciente paciente, AuditoriaService? auditoria, string databasePath)
        {
            ArgumentNullException.ThrowIfNull(paciente);
            if (auditoria == null) throw new InvalidOperationException("No se puede guardar sin el servicio de auditoría.");
            using (var schema = GetConnection(databasePath)) { }

            var originalId = paciente.Id;
            var savedId = originalId;
            var createdAt = originalId == 0 ? DateTime.Now : paciente.FechaCreacion;
            auditoria.RegistrarEventoConOperacion(databasePath, connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = originalId == 0
                    ? @"INSERT INTO Paciente (NombreCompleto, DNI, Telefono, Email, FechaNacimiento, Sexo, Calle, Numero, Piso, CP, Municipio, Provincia, ProteccionDatos, Firma, FechaAlta, FechaCreacion)
                        VALUES (@nombre, @dni, @telefono, @email, @nacimiento, @sexo, @calle, @numero, @piso, @cp, @municipio, @provincia, @proteccion, @firma, @alta, @creacion);"
                    : @"UPDATE Paciente SET NombreCompleto=@nombre, DNI=@dni, Telefono=@telefono, Email=@email, FechaNacimiento=@nacimiento, Sexo=@sexo,
                        Calle=@calle, Numero=@numero, Piso=@piso, CP=@cp, Municipio=@municipio, Provincia=@provincia, ProteccionDatos=@proteccion,
                        Firma=@firma, FechaAlta=@alta, FechaCreacion=@creacion WHERE Id=@id;";
                void Add(string name, object? value) => command.Parameters.AddWithValue(name, value ?? DBNull.Value);
                Add("@nombre", paciente.NombreCompleto);
                Add("@dni", paciente.DNI);
                Add("@telefono", paciente.Telefono);
                Add("@email", paciente.Email);
                // Preserve sqlite-net's ticks representation, including nullable dates.
                Add("@nacimiento", paciente.FechaNacimiento?.Ticks);
                Add("@sexo", paciente.Sexo);
                Add("@calle", paciente.Calle);
                Add("@numero", paciente.Numero);
                Add("@piso", paciente.Piso);
                Add("@cp", paciente.CP);
                Add("@municipio", paciente.Municipio);
                Add("@provincia", paciente.Provincia);
                Add("@proteccion", paciente.ProteccionDatos);
                Add("@firma", paciente.Firma);
                Add("@alta", paciente.FechaAlta?.Ticks);
                Add("@creacion", createdAt.Ticks);
                if (originalId != 0) Add("@id", originalId);
                command.ExecuteNonQuery();
                if (originalId == 0)
                {
                    command.CommandText = "SELECT last_insert_rowid();";
                    savedId = checked(Convert.ToInt32(command.ExecuteScalar()));
                }
                return CreateEvent(originalId == 0 ? "Paciente.Crear" : "Paciente.Editar", savedId, paciente.NombreCompleto);
            });
            // Failed writes must not leave an ID or creation timestamp that appears committed.
            paciente.Id = savedId;
            paciente.FechaCreacion = createdAt;
        }

        public static void Eliminar(int id)
            => Eliminar(id, App.AuditoriaService, dbPath);

        internal static void Eliminar(int id, AuditoriaService? auditoria, string databasePath)
        {
            if (auditoria == null) throw new InvalidOperationException("No se puede eliminar sin el servicio de auditoría.");
            using (var schema = GetConnection(databasePath)) { }
            auditoria.RegistrarEventoConOperacion(databasePath, connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM Paciente WHERE Id=@id;";
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
                return CreateEvent("Paciente.Eliminar", id, id.ToString());
            });
        }

        private static AuditoriaEvento CreateEvent(string action, int id, string? affectedUser) => new AuditoriaEvento
        {
            UsuarioAdmin = Sesion.UsuarioActual ?? "Sistema",
            Accion = action,
            Modulo = "Recepción",
            UsuarioAfectado = affectedUser ?? string.Empty,
            Resultado = true,
            FechaHora = DateTime.Now,
            Detalles = AuditoriaDetallesHelper.CrearJson(("PacienteId", id)),
            Tipo = "Paciente",
            Rol = Sesion.RolActual,
            Area = Sesion.AreaActual
        };
        public static Paciente? ObtenerPorId(int id)
        {
            using var conn = GetConnection();
            return conn.Table<Paciente>().FirstOrDefault(p => p.Id == id);
        }
    }
}
