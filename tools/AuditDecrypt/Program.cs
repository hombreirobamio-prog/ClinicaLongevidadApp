using System;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace AuditDecryptTool
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: AuditDecrypt <ConnectionString> [maxRows]");
                return 1;
            }

            var connStr = args[0];
            int max = 50;
            if (args.Length >= 2 && int.TryParse(args[1], out var m)) max = m;

            var encKey = GetEncryptionKeyFromEnv();

            try
            {
                using var conn = new SqliteConnection(connStr);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, EventId, Fechahora, UsuarioAdmin, Accion, Modulo, UsuarioAfectado, Resultado, Detalles, DetallesPlain, DetallesEnc, KeyVersionEnc FROM Auditoria ORDER BY Id DESC LIMIT @max";
                cmd.Parameters.AddWithValue("@max", max);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var id = rdr.GetInt32(0);
                    var accion = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4);
                    var detalles = rdr.IsDBNull(8) ? string.Empty : rdr.GetString(8);
                    var detallesPlain = rdr.IsDBNull(9) ? string.Empty : rdr.GetString(9);
                    var detallesEnc = rdr.IsDBNull(10) ? string.Empty : rdr.GetString(10);
                    var keyVerEnc = rdr.IsDBNull(11) ? string.Empty : rdr.GetString(11);

                    string payload = detallesPlain;
                    if (string.IsNullOrWhiteSpace(payload))
                    {
                        if (!string.IsNullOrWhiteSpace(detalles) && !IsBase64(detalles))
                        {
                            payload = detalles;
                        }
                        else if (!string.IsNullOrWhiteSpace(detallesEnc) || IsBase64(detalles))
                        {
                            var blob = !string.IsNullOrWhiteSpace(detallesEnc) ? detallesEnc : detalles;
                            try
                            {
                                var combined = Convert.FromBase64String(blob);
                                if (encKey != null && encKey.Length > 0)
                                {
                                    var nonce = new byte[12];
                                    var tag = new byte[16];
                                    var cipher = new byte[combined.Length - nonce.Length - tag.Length];
                                    Buffer.BlockCopy(combined, 0, nonce, 0, nonce.Length);
                                    Buffer.BlockCopy(combined, nonce.Length, tag, 0, tag.Length);
                                    Buffer.BlockCopy(combined, nonce.Length + tag.Length, cipher, 0, cipher.Length);
                                    var plain = new byte[cipher.Length];
                                    using (var aesg = new System.Security.Cryptography.AesGcm(encKey, 16))
                                    {
                                        aesg.Decrypt(nonce, cipher, tag, plain);
                                    }
                                    payload = Encoding.UTF8.GetString(plain);
                                }
                            }
                            catch
                            {
                                payload = blob; // fallback
                            }
                        }
                    }

                    Console.WriteLine($"Id={id}\tAccion={accion}\n{payload}\n---\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed: " + ex.Message);
                return 2;
            }

            return 0;
        }

        static bool IsBase64(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            Span<byte> buffer = new byte[s.Length];
            return Convert.TryFromBase64String(s, buffer, out _);
        }

        static byte[]? GetEncryptionKeyFromEnv()
        {
            var v = Environment.GetEnvironmentVariable("AUDIT_ENC_KEY");
            if (string.IsNullOrEmpty(v)) return null;
            try
            {
                var maybe = Convert.FromBase64String(v);
                if (maybe.Length >= 16) return maybe;
            }
            catch { }
            return Encoding.UTF8.GetBytes(v);
        }
    }
}
