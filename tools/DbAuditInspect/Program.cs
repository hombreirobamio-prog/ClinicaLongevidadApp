using System;
using System.Data;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace DbAuditInspect
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: dotnet run --project tools/DbAuditInspect -- <path-to-db> [out.json]");
                return 1;
            }

            var dbPath = args[0];
            var outPath = args.Length > 1 ? args[1] : "audit_rows_export.json";

            if (!File.Exists(dbPath))
            {
                Console.Error.WriteLine($"Database not found: {dbPath}");
                return 2;
            }

            try
            {
                var connStr = $"Data Source={dbPath}";
                using var conn = new SqliteConnection(connStr);
                conn.Open();

                // Select rows that likely contain plaintext details or explicit DetallesPlain
                var sql = @"SELECT Id, Fechahora, UsuarioAdmin, Accion, Modulo, Resultado, Detalles, DetallesPlain, DetallesEnc
FROM Auditoria
WHERE (DetallesPlain IS NOT NULL AND TRIM(DetallesPlain) <> '')
   OR (Detalles IS NOT NULL AND TRIM(Detalles) LIKE '{%')
ORDER BY Id;";

                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;

                using var reader = cmd.ExecuteReader();
                using var sw = new StreamWriter(outPath, false, System.Text.Encoding.UTF8);

                var options = new JsonSerializerOptions { WriteIndented = true };

                while (reader.Read())
                {
                    var row = new
                    {
                        Id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        FechaHora = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        UsuarioAdmin = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Accion = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Modulo = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        Resultado = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        Detalles = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        DetallesPlain = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                        DetallesEnc = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
                    };

                    sw.WriteLine(JsonSerializer.Serialize(row, options));
                }

                Console.WriteLine($"Export finished: {outPath}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                return 3;
            }
        }
    }
}
