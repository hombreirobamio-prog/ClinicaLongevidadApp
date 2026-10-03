using System;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace ClinicaLongevidadApp.Services
{
    public class AzureKeyRotationProvider : IKeyRotationProvider
    {
        private readonly SecretClient _client;
        private readonly string? _hmacSecretName;
        private readonly string? _encSecretName;

        public AzureKeyRotationProvider()
        {
            string? vaultUri = Environment.GetEnvironmentVariable("KEYVAULT_URI");
            _hmacSecretName = Environment.GetEnvironmentVariable("AUDIT_HMAC_SECRET_NAME");
            _encSecretName = Environment.GetEnvironmentVariable("AUDIT_ENC_SECRET_NAME");

            if (string.IsNullOrWhiteSpace(vaultUri))
                throw new InvalidOperationException("KEYVAULT_URI is not configured for AzureKeyRotationProvider");

            _client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
        }

        public void PersistHmacKey(byte[] key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (string.IsNullOrWhiteSpace(_hmacSecretName)) throw new InvalidOperationException("AUDIT_HMAC_SECRET_NAME is not configured");

            string value = Convert.ToBase64String(key);
            var resp = _client.SetSecret(_hmacSecretName, value);
            // Optionally set process environment so running app picks it up.
            try
            {
                Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", value, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY_VERSION", resp.Value.Properties.Version, EnvironmentVariableTarget.Process);
            }
            catch { }
        }

        public void PersistEncryptionKey(byte[] key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (string.IsNullOrWhiteSpace(_encSecretName)) throw new InvalidOperationException("AUDIT_ENC_SECRET_NAME is not configured");

            string value = Convert.ToBase64String(key);
            var resp = _client.SetSecret(_encSecretName, value);
            try
            {
                Environment.SetEnvironmentVariable("AUDIT_ENC_KEY", value, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("AUDIT_ENC_KEY_VERSION", resp.Value.Properties.Version, EnvironmentVariableTarget.Process);
            }
            catch { }
        }
    }
}
