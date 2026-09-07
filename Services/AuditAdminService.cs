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
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var tran = conn.BeginTransaction();
                using var ins = conn.CreateCommand();
                ins.Transaction = tran;
                ins.CommandText = @"INSERT INTO AuditForwardQueue (EventId, Payload, Signature, Attempts, NextAttemptAt, CreatedAt)
                                    SELECT EventId, Payload, Signature, 0, @next, CreatedAt FROM AuditForwardDeadLetter WHERE Id = @id";
                ins.Parameters.AddWithValue("@next", DateTime.UtcNow.ToString("o"));
                ins.Parameters.AddWithValue("@id", deadId);
                ins.ExecuteNonQuery();

                using var del = conn.CreateCommand();
                del.Transaction = tran;
                del.CommandText = "DELETE FROM AuditForwardDeadLetter WHERE Id = @id";
                del.Parameters.AddWithValue("@id", deadId);
                del.ExecuteNonQuery();

                tran.Commit();
                return true;
            }
            catch (Exception ex)
            {
                try { LogService.Error("AuditAdminService", "Failed to requeue dead letter", ex); } catch { }
            }
            return false;
        }

        public bool DeleteDeadLetter(int deadId)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM AuditForwardDeadLetter WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", deadId);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                try { LogService.Error("AuditAdminService", "Failed to delete dead letter", ex); } catch { }
            }
            return false;
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
