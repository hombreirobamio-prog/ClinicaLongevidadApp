using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace ClinicaLongevidadApp.Services
{
    public class AuditAdminService
    {
        private readonly string _connectionString;

        public AuditAdminService(string connectionString)
        {
            _connectionString = connectionString;
        }

    public class AuditRecentDto
    {
        public int Id { get; set; }
        public string EventId { get; set; } = string.Empty;
        public string FechaHora { get; set; } = string.Empty;
        public string UsuarioAdmin { get; set; } = string.Empty;
        public string Accion { get; set; } = string.Empty;
        public string Modulo { get; set; } = string.Empty;
        public string UsuarioAfectado { get; set; } = string.Empty;
        public string Resultado { get; set; } = string.Empty;
        // Additional metadata columns so the admin service can provide full rows for the UI
        public string Detalles { get; set; } = string.Empty;
        public string DetallesPlain { get; set; } = string.Empty;
        public string DetallesEnc { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string SesionId { get; set; } = string.Empty;
        public string Equipo { get; set; } = string.Empty;
        public string VersionApp { get; set; } = string.Empty;
        public string KeyVersion { get; set; } = string.Empty;
        public string KeyVersionEnc { get; set; } = string.Empty;
    }

        public (int Pending, int DeadLetter) GetCounts()
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT (SELECT COUNT(1) FROM AuditForwardQueue), (SELECT COUNT(1) FROM AuditForwardDeadLetter)";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var p = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    var d = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    return (p, d);
                }
            }
            catch { }
            return (0, 0);
        }

        public List<AuditQueueRowDto> GetPending(int limit = 100)
        {
            var list = new List<AuditQueueRowDto>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, EventId, Payload, Signature, Attempts, LastError, NextAttemptAt, CreatedAt FROM AuditForwardQueue ORDER BY Id DESC LIMIT @max";
                cmd.Parameters.AddWithValue("@max", limit);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new AuditQueueRowDto
                    {
                        Id = reader.IsDBNull(0) ? -1 : reader.GetInt32(0),
                        EventId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        Payload = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Signature = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Attempts = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                        LastError = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        NextAttemptAt = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        CreatedAt = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                    });
                }
            }
            catch { }
            return list;
        }

        public List<AuditRecentDto> GetRecentAudits(int limit = 50)
        {
            var list = new List<AuditRecentDto>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                // Include metadata columns so admin read paths can populate the full model used by the UI
                if (limit > 0)
                {
                    cmd.CommandText = "SELECT Id, EventId, Fechahora, UsuarioAdmin, Accion, Modulo, UsuarioAfectado, Resultado, Detalles, DetallesPlain, DetallesEnc, Tipo, Rol, Area, SesionId, Equipo, VersionApp, KeyVersion, KeyVersionEnc FROM Auditoria ORDER BY Fechahora DESC LIMIT @max";
                    cmd.Parameters.AddWithValue("@max", limit);
                }
                else
                {
                    cmd.CommandText = "SELECT Id, EventId, Fechahora, UsuarioAdmin, Accion, Modulo, UsuarioAfectado, Resultado, Detalles, DetallesPlain, DetallesEnc, Tipo, Rol, Area, SesionId, Equipo, VersionApp, KeyVersion, KeyVersionEnc FROM Auditoria ORDER BY Fechahora DESC";
                }
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new AuditRecentDto
                    {
                        Id = reader.IsDBNull(0) ? -1 : reader.GetInt32(0),
                        EventId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        FechaHora = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        UsuarioAdmin = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Accion = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        Modulo = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        UsuarioAfectado = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        Resultado = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                        Detalles = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                        DetallesPlain = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                        DetallesEnc = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                        Tipo = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                        Rol = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                        Area = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                        SesionId = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
                        Equipo = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                        VersionApp = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                        KeyVersion = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
                        KeyVersionEnc = reader.IsDBNull(18) ? string.Empty : reader.GetString(18)
                    });
                }
            }
            catch { }
            return list;
        }

        public List<AuditQueueRowDto> GetDeadLetter(int limit = 100)
        {
            var list = new List<AuditQueueRowDto>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, EventId, Payload, Signature, Attempts, LastError, CreatedAt, FailedAt FROM AuditForwardDeadLetter ORDER BY FailedAt DESC LIMIT @max";
                cmd.Parameters.AddWithValue("@max", limit);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new AuditQueueRowDto
                    {
                        Id = reader.IsDBNull(0) ? -1 : reader.GetInt32(0),
                        EventId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        Payload = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Signature = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Attempts = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                        LastError = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        CreatedAt = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        FailedAt = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                    });
                }
            }
            catch { }
            return list;
        }

        public bool RequeueDeadLetter(int deadId)
            => RequeueDeadLetter(deadId, App.AuditoriaService);

        internal bool RequeueDeadLetter(int deadId, AuditoriaService? auditoria)
            => ChangeDeadLetter(deadId, true, auditoria);

        public bool DeleteDeadLetter(int deadId)
            => DeleteDeadLetter(deadId, App.AuditoriaService);

        internal bool DeleteDeadLetter(int deadId, AuditoriaService? auditoria)
            => ChangeDeadLetter(deadId, false, auditoria);

        private bool ChangeDeadLetter(int deadId, bool requeue, AuditoriaService? auditoria)
        {
            try
            {
                AuthorizationHelper.EnsureRole("Administración");
                if (auditoria == null) throw new InvalidOperationException("No se puede modificar la cola sin el servicio de auditoría.");
                var databasePath = new SqliteConnectionStringBuilder(_connectionString).DataSource;
                auditoria.RegistrarEventoConOperacion(databasePath, connection =>
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT EventId FROM AuditForwardDeadLetter WHERE Id=@id;";
                    command.Parameters.AddWithValue("@id", deadId);
                    var eventId = command.ExecuteScalar() as string
                        ?? throw new InvalidOperationException("El registro de la cola de fallidos ya no existe.");

                    if (requeue)
                    {
                        command.CommandText = @"INSERT INTO AuditForwardQueue (EventId, Payload, Signature, Attempts, NextAttemptAt, CreatedAt)
                            SELECT EventId, Payload, Signature, 0, @next, CreatedAt FROM AuditForwardDeadLetter WHERE Id=@id;";
                        command.Parameters.AddWithValue("@next", DateTime.UtcNow.ToString("o"));
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("No se pudo reencolar el registro.");
                    }
                    command.CommandText = "DELETE FROM AuditForwardDeadLetter WHERE Id=@id;";
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("No se pudo retirar el registro de la cola de fallidos.");

                    return new Models.AuditoriaEvento
                    {
                        UsuarioAdmin = Sesion.UsuarioActual ?? "Sistema",
                        Accion = requeue ? "AuditQueue.Reencolar" : "AuditQueue.Eliminar",
                        Modulo = "Auditoría",
                        UsuarioAfectado = eventId,
                        Resultado = true,
                        FechaHora = DateTime.Now,
                        Tipo = "Operación",
                        // Reference the affected event without copying its payload or signature.
                        Detalles = AuditoriaDetallesHelper.CrearJson(("DeadLetterId", deadId), ("EventIdAfectado", eventId)),
                        Rol = Sesion.RolActual,
                        Area = Sesion.AreaActual
                    };
                });
                return true;
            }
            catch (Exception ex)
            {
                try { AuditLogHelper.Error("AuditAdminService", "Failed to change dead letter", ex); } catch { }
                return false;
            }
        }
    }

    public class AuditQueueRowDto
    {
        public int Id { get; set; }
        public string EventId { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public int Attempts { get; set; }
        public string LastError { get; set; } = string.Empty;
        public string NextAttemptAt { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string FailedAt { get; set; } = string.Empty;
    }
}
