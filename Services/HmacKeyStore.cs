using System;
using System.IO;
using System.Security.Cryptography;

namespace ClinicaLongevidadApp.Services
{
    public static class HmacKeyStore
    {
        // Ruta: %LOCALAPPDATA%\ClinicaLongevidadApp\keys\hmac.key
        public static string GetKeyFilePath()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "ClinicaLongevidadApp", "keys", "hmac.key");
        }

        public static void SaveEncryptedKey(byte[] keyBytes)
        {
            var path = GetKeyFilePath();
            var dir = Path.GetDirectoryName(path) ?? string.Empty;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var protectedBytes = ProtectedData.Protect(keyBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, protectedBytes);
        }

        public static bool TryLoadDecryptedKey(out byte[] keyBytes)
        {
            keyBytes = Array.Empty<byte>();
            var path = GetKeyFilePath();
            if (!File.Exists(path)) return false;

            try
            {
                var protectedBytes = File.ReadAllBytes(path);
                keyBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                return keyBytes != null && keyBytes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        // Convenience: load plaintext base64/hex file if exists (unsafe — only fallback)
        public static bool TryLoadPlaintextKey(out byte[] keyBytes)
        {
            keyBytes = Array.Empty<byte>();
            var path = Path.ChangeExtension(GetKeyFilePath(), ".txt");
            if (!File.Exists(path)) return false;
            try
            {
                var text = File.ReadAllText(path).Trim();
                // Try Base64
                try { keyBytes = Convert.FromBase64String(text); return true; } catch { }
                // Try hex (rudimentary)
                try
                {
                    var s = text.Replace("0x", "", StringComparison.OrdinalIgnoreCase).Replace("-", "").Replace(":", "").Replace(" ", "");
                    if (s.Length % 2 != 0) return false;
                    var b = new byte[s.Length / 2];
                    for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
                    keyBytes = b;
                    return true;
                }
                catch { return false; }
            }
            catch { return false; }
        }
    }
}
