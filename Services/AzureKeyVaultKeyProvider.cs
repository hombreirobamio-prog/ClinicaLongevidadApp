using System;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace ClinicaLongevidadApp.Services
{
    /// <summary>
    /// Key provider that reads HMAC and encryption keys from Azure Key Vault.
    /// Uses DefaultAzureCredential (Managed Identity when available).
    /// Configuration via environment variables:
    /// - KEYVAULT_URI (e.g. https://myvault.vault.azure.net/)
    /// - AUDIT_HMAC_SECRET_NAME
    /// - AUDIT_ENC_SECRET_NAME
    /// </summary>
    public class AzureKeyVaultKeyProvider : IKeyProvider
    {
        private readonly SecretClient _client;
        private readonly string? _hmacSecretName;
        private readonly string? _encSecretName;

        public AzureKeyVaultKeyProvider()
        {
            string? vaultUri = Environment.GetEnvironmentVariable("KEYVAULT_URI");
            _hmacSecretName = Environment.GetEnvironmentVariable("AUDIT_HMAC_SECRET_NAME");
            _encSecretName = Environment.GetEnvironmentVariable("AUDIT_ENC_SECRET_NAME");

            if (string.IsNullOrWhiteSpace(vaultUri))
                throw new InvalidOperationException("KEYVAULT_URI is not configured for AzureKeyVaultKeyProvider");

            _client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
        }

        public byte[]? GetHmacKey()
        {
            if (string.IsNullOrWhiteSpace(_hmacSecretName)) return null;

            try
            {
                var secret = _client.GetSecret(_hmacSecretName);
                string value = secret.Value.Value ?? string.Empty;
                // support base64 or raw
                try { return Convert.FromBase64String(value); } catch { return System.Text.Encoding.UTF8.GetBytes(value); }
            }
            catch
            {
                return null;
            }
        }

        public byte[]? GetEncryptionKey()
        {
            if (string.IsNullOrWhiteSpace(_encSecretName)) return null;

            try
            {
                var secret = _client.GetSecret(_encSecretName);
                string value = secret.Value.Value ?? string.Empty;
                try { return Convert.FromBase64String(value); } catch { return System.Text.Encoding.UTF8.GetBytes(value); }
            }
            catch
            {
                return null;
            }
        }

        public string? GetHmacKeyVersion()
        {
            if (string.IsNullOrWhiteSpace(_hmacSecretName)) return null;
            try
            {
                var secret = _client.GetSecret(_hmacSecretName);
                return secret.Value.Properties.Version;
            }
            catch
            {
                return null;
            }
        }

        public string? GetEncryptionKeyVersion()
        {
            if (string.IsNullOrWhiteSpace(_encSecretName)) return null;
            try
            {
                var secret = _client.GetSecret(_encSecretName);
                return secret.Value.Properties.Version;
            }
            catch
            {
                return null;
            }
        }

        public byte[]? GetHmacKeyByVersion(string? version)
        {
            if (string.IsNullOrWhiteSpace(_hmacSecretName) || string.IsNullOrWhiteSpace(version)) return null;

            try
            {
                var secret = _client.GetSecret(_hmacSecretName, version);
                string value = secret.Value.Value ?? string.Empty;
                try { return Convert.FromBase64String(value); } catch { return System.Text.Encoding.UTF8.GetBytes(value); }
            }
            catch
            {
                return null;
            }
        }

        public byte[]? GetEncryptionKeyByVersion(string? version)
        {
            if (string.IsNullOrWhiteSpace(_encSecretName) || string.IsNullOrWhiteSpace(version)) return null;

            try
            {
                var secret = _client.GetSecret(_encSecretName, version);
                string value = secret.Value.Value ?? string.Empty;
                try { return Convert.FromBase64String(value); } catch { return System.Text.Encoding.UTF8.GetBytes(value); }
            }
            catch
            {
                return null;
            }
        }
    }
}
