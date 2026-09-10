using System;
using Microsoft.Data.Sqlite;

class Program
{
    static int Main(string[] args)
    {
        string dbPath;
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            dbPath = args[0];
        }
        else
        {
            dbPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicaLongevidad.db");
        }

        Console.WriteLine($"Checking DB at: {dbPath}");

        if (!System.IO.File.Exists(dbPath))
        {
            Console.WriteLine("ERROR: Database file not found.");
            return 2;
        }

        try
        {
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, NombreUsuario, Rol, Area, Activo, PasswordHash FROM Usuario WHERE lower(NombreUsuario) = 'admin' LIMIT 1;";

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                Console.WriteLine("NOTFOUND");
                return 0;
            }

            var id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            var nombre = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var rol = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var area = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var activo = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
            var pwd = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);

            Console.WriteLine("FOUND");
            Console.WriteLine($"Id: {id}");
            Console.WriteLine($"NombreUsuario: {nombre}");
            Console.WriteLine($"Rol: {rol}");
            Console.WriteLine($"Area: {area}");
            Console.WriteLine($"Activo: {activo}");
            Console.WriteLine($"PasswordHashPresent: {(!string.IsNullOrEmpty(pwd))}");
            Console.WriteLine($"PasswordHashPreview: {(pwd?.Length>20 ? pwd.Substring(0,20)+"..." : pwd)}");
            Console.WriteLine($"PasswordLooksHashed: {(pwd?.StartsWith("PBKDF2$") == true)}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            return 3;
        }
    }
}
