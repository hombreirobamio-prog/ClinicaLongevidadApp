using System;
using System.Text;

namespace ClinicaLongevidadApp.Services
{
    /// <summary>
    /// Local key provider that reads keys from environment variables.
    /// Intended for development/testing only. Production should use a managed Key Vault.
    /// </summary>
    public class LocalKeyProvider : IKeyProvider
    {
        private const string HmacEnv = "AUDIT_HMAC_KEY";
        private const string EncEnv = "AUDIT_ENC_KEY";
        private const string HmacVerEnv = "AUDIT_HMAC_KEY_VERSION";
        private const string EncVerEnv = "AUDIT_ENC_KEY_VERSION";

        public byte[]? GetHmacKey()
        {
            var v = Environment.GetEnvironmentVariable(HmacEnv);
            if (string.IsNullOrEmpty(v)) return null;
            // Try base64 decode first (rotation writes base64). Fall back to raw UTF8 bytes.
            try
            {
                var maybe = Convert.FromBase64String(v);
                if (maybe.Length > 0) return maybe;
            }
            catch { }
            return Encoding.UTF8.GetBytes(v);
        }

        public byte[]? GetEncryptionKey()
        {
            var v = Environment.GetEnvironmentVariable(EncEnv);
            if (string.IsNullOrEmpty(v)) return null;
            // Expect base64 encoded 32-byte key or raw string
            try
            {
                var maybe = Convert.FromBase64String(v);
                if (maybe.Length >= 16) return maybe;
            }
            catch { }
            return Encoding.UTF8.GetBytes(v);
        }

        public string? GetHmacKeyVersion()
        {
            var v = Environment.GetEnvironmentVariable(HmacVerEnv);
            if (!string.IsNullOrEmpty(v)) return v;
            return "local";
        }

        public string? GetEncryptionKeyVersion()
        {
            var v = Environment.GetEnvironmentVariable(EncVerEnv);
            if (!string.IsNullOrEmpty(v)) return v;
            return "local";
        }

        public byte[]? GetHmacKeyByVersion(string? version)
        {
            // Local provider only keeps the current key in environment; return it if versions match or if version is null/"local"
            var currentVer = GetHmacKeyVersion();
            if (string.IsNullOrEmpty(version) || string.Equals(version, currentVer, StringComparison.OrdinalIgnoreCase) || string.Equals(version, "local", StringComparison.OrdinalIgnoreCase))
            {
                return GetHmacKey();
            }

            return null;
        }

        public byte[]? GetEncryptionKeyByVersion(string? version)
        {
            var currentVer = GetEncryptionKeyVersion();
            if (string.IsNullOrEmpty(version) || string.Equals(version, currentVer, StringComparison.OrdinalIgnoreCase) || string.Equals(version, "local", StringComparison.OrdinalIgnoreCase))
            {
                return GetEncryptionKey();
            }

            return null;
        }
    }
}
