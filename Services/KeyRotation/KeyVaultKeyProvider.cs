namespace ClinicaLongevidadApp.Services.KeyRotation
{
    using System;
    using System.Threading.Tasks;
    using Azure;
    using Azure.Identity;
    using Azure.Security.KeyVault.Secrets;

    /// <summary>
    /// Key Vault backed key provider using Azure SDK.
    /// Secrets are stored as Base64-encoded values under well-known secret names.
    /// </summary>
    public class KeyVaultKeyProvider
    {
        private readonly string? _vaultUri;
        private readonly SecretClient? _client;

        public KeyVaultKeyProvider()
        {
            _vaultUri = Environment.GetEnvironmentVariable("KEYVAULT_URI");
            if (!string.IsNullOrWhiteSpace(_vaultUri))
            {
                _client = new SecretClient(new Uri(_vaultUri), new DefaultAzureCredential());
            }
        }

        public bool IsConfigured => _client != null;

        private const string HmacSecretName = "audit-hmac-key";
        private const string EncSecretName = "audit-enc-key";

        public async Task<byte[]?> GetHmacKeyAsync(string? version = null)
        {
            if (_client == null) return null;
            try
            {
                Response<KeyVaultSecret> secretResp = string.IsNullOrWhiteSpace(version)
                    ? await _client.GetSecretAsync(HmacSecretName)
                    : await _client.GetSecretAsync(HmacSecretName, version);

                var secret = secretResp.Value;
                if (string.IsNullOrWhiteSpace(secret.Value)) return null;
                return Convert.FromBase64String(secret.Value);
            }
            catch (RequestFailedException)
            {
                return null;
            }
        }

        public async Task<byte[]?> GetEncryptionKeyAsync(string? version = null)
        {
            if (_client == null) return null;
            try
            {
                Response<KeyVaultSecret> secretResp = string.IsNullOrWhiteSpace(version)
                    ? await _client.GetSecretAsync(EncSecretName)
                    : await _client.GetSecretAsync(EncSecretName, version);

                var secret = secretResp.Value;
                if (string.IsNullOrWhiteSpace(secret.Value)) return null;
                return Convert.FromBase64String(secret.Value);
            }
            catch (RequestFailedException)
            {
                return null;
            }
        }

        public async Task<string?> SetHmacKeyAsync(byte[] key, string? versionTag = null)
        {
            if (_client == null) throw new InvalidOperationException("KeyVault not configured");
            var value = Convert.ToBase64String(key);
            var secret = new KeyVaultSecret(HmacSecretName, value);
            if (!string.IsNullOrWhiteSpace(versionTag))
            {
                secret.Properties.Tags["keyVersion"] = versionTag;
            }

            var resp = await _client.SetSecretAsync(secret);
            return resp.Value.Properties.Version;
        }

        public async Task<string?> SetEncryptionKeyAsync(byte[] key, string? versionTag = null)
        {
            if (_client == null) throw new InvalidOperationException("KeyVault not configured");
            var value = Convert.ToBase64String(key);
            var secret = new KeyVaultSecret(EncSecretName, value);
            if (!string.IsNullOrWhiteSpace(versionTag))
            {
                secret.Properties.Tags["keyVersion"] = versionTag;
            }

            var resp = await _client.SetSecretAsync(secret);
            return resp.Value.Properties.Version;
        }

        public async Task<string?> GetLatestHmacKeyVersionAsync()
        {
            if (_client == null) return null;
            try
            {
                var resp = await _client.GetSecretAsync(HmacSecretName);
                return resp.Value.Properties.Version;
            }
            catch (RequestFailedException)
            {
                return null;
            }
        }

        public async Task<string?> GetLatestEncryptionKeyVersionAsync()
        {
            if (_client == null) return null;
            try
            {
                var resp = await _client.GetSecretAsync(EncSecretName);
                return resp.Value.Properties.Version;
            }
            catch (RequestFailedException)
            {
                return null;
            }
        }
    }
}
