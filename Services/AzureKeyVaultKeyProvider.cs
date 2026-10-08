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
                return DecodeHmacSecret(secret.Value.Value);
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
                return DecodeEncryptionSecret(secret.Value.Value);
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
                return DecodeHmacSecret(secret.Value.Value);
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
                return DecodeEncryptionSecret(secret.Value.Value);
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? DecodeHmacSecret(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            try
            {
                var key = Convert.FromBase64String(value);
                return key.Length >= 32 ? key : null;
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? DecodeEncryptionSecret(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            try
            {
                var key = Convert.FromBase64String(value);
                return key.Length is 16 or 24 or 32 ? key : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
