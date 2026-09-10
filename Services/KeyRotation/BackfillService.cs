namespace ClinicaLongevidadApp.Services.KeyRotation
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;

    /// <summary>
    /// Safe backfill service: creates new audit rows that contain encrypted details
    /// based on existing plaintext/legacy rows. This preserves original rows and
    /// appends new audit events produced via AuditoriaService to keep the original
    /// chain intact.
    /// </summary>
    public class BackfillService
    {
        private readonly string _connectionString;
        private readonly Services.AuditoriaService _auditoriaService;

        public BackfillService(string connectionString)
        {
            _connectionString = connectionString;
            _auditoriaService = new Services.AuditoriaService(connectionString);
        }

        /// <summary>
        /// Return an estimate (count) of rows that appear to contain plaintext details and
        /// that have not yet been backfilled.
        /// </summary>
        public Task<long> PreviewBackfillCountAsync()
        {
            long count = 0;
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT COUNT(1) FROM Auditoria a
WHERE (DetallesPlain IS NOT NULL AND TRIM(DetallesPlain) <> '')
   OR (Detalles IS NOT NULL AND TRIM(Detalles) LIKE '{%')";
            var v = cmd.ExecuteScalar();
            count = v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);

            return Task.FromResult(count);
        }

        /// <summary>
        /// Apply backfill by creating audit events for rows with plaintext details.
        /// This method is safe in that it does not modify existing rows; it appends new events.
        /// </summary>
        public async Task<BackfillResult> ApplyBackfillAsync(int batchSize = 100, bool dryRun = true)
        {
            var processed = 0;
            var created = 0;
            var skipped = 0;

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var selectCmd = conn.CreateCommand();
            selectCmd.CommandText = @"SELECT Id, UsuarioAdmin, Accion, Fechahora, Modulo, UsuarioAfectado, Resultado, Detalles, DetallesPlain
FROM Auditoria
WHERE (DetallesPlain IS NOT NULL AND TRIM(DetallesPlain) <> '')
   OR (Detalles IS NOT NULL AND TRIM(Detalles) LIKE '{%')
ORDER BY Id LIMIT @limit";
            selectCmd.Parameters.AddWithValue("@limit", batchSize);

            using var reader = selectCmd.ExecuteReader();
            var toProcess = new List<(long Id, string detalles)>();
            while (reader.Read())
            {
                var id = reader.IsDBNull(0) ? 0L : reader.GetInt64(0);
                var detalles = reader.IsDBNull(8) ? (reader.IsDBNull(7) ? string.Empty : reader.GetString(7)) : reader.GetString(8);
                toProcess.Add((id, detalles));
            }

            processed = toProcess.Count;

            foreach (var item in toProcess)
            {
                // Compose a new audit event that records the backfill action and includes the original details.
                var detalleObj = new Dictionary<string, object>
                {
                    ["BackfilledFromId"] = item.Id,
                    ["OriginalDetalles"] = item.detalles
                };

                var evento = new Models.AuditoriaEvento
                {
                    Accion = "Backfill.Reencrypt",
                    Modulo = "KeyRotation",
                    UsuarioAdmin = Environment.UserName ?? "backfill-tool",
                    Resultado = true,
                    Tipo = "Backfill",
                    FechaHora = DateTime.Now,
                    Detalles = JsonSerializer.Serialize(detalleObj)
                };

                if (!dryRun)
                {
                    try
                    {
                        _auditoriaService.RegistrarEvento(evento);
                        created++;
                    }
                    catch
                    {
                        skipped++;
                    }
                }
            }

            return new BackfillResult { Processed = processed, Created = created, Skipped = skipped };
        }
    }

    public class BackfillResult
    {
        public int Processed { get; set; }
        public int Created { get; set; }
        public int Skipped { get; set; }
    }
}
