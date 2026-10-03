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
                if (!Uri.TryCreate(_vaultUri, UriKind.Absolute, out var vaultUri)
                    || !string.Equals(vaultUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(vaultUri.Host))
                {
                    throw new InvalidOperationException("KEYVAULT_URI debe ser una dirección HTTPS válida, por ejemplo https://mi-almacen.vault.azure.net/.");
                }

                _client = new SecretClient(vaultUri, new DefaultAzureCredential());
            }
        }

        private readonly string? _hmacSecretName = Environment.GetEnvironmentVariable("AUDIT_HMAC_SECRET_NAME");
        private readonly string? _encSecretName = Environment.GetEnvironmentVariable("AUDIT_ENC_SECRET_NAME");

        public bool IsConfigured => _client != null
            && !string.IsNullOrWhiteSpace(_hmacSecretName)
            && !string.IsNullOrWhiteSpace(_encSecretName);

        public async Task<byte[]?> GetHmacKeyAsync(string? version = null)
        {
            if (!IsConfigured) return null;
            var client = _client!;
            var secretName = _hmacSecretName!;
            try
            {
                Response<KeyVaultSecret> secretResp = string.IsNullOrWhiteSpace(version)
                    ? await client.GetSecretAsync(secretName)
                    : await client.GetSecretAsync(secretName, version);

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
            if (!IsConfigured) return null;
            var client = _client!;
            var secretName = _encSecretName!;
            try
            {
                Response<KeyVaultSecret> secretResp = string.IsNullOrWhiteSpace(version)
                    ? await client.GetSecretAsync(secretName)
                    : await client.GetSecretAsync(secretName, version);

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
            if (!IsConfigured) throw new InvalidOperationException("Key Vault or AUDIT_HMAC_SECRET_NAME/AUDIT_ENC_SECRET_NAME is not configured.");
            var client = _client!;
            var value = Convert.ToBase64String(key);
            var secret = new KeyVaultSecret(_hmacSecretName!, value);
            if (!string.IsNullOrWhiteSpace(versionTag))
            {
                secret.Properties.Tags["keyVersion"] = versionTag;
            }

            var resp = await client.SetSecretAsync(secret);
            return resp.Value.Properties.Version;
        }

        public async Task<string?> SetEncryptionKeyAsync(byte[] key, string? versionTag = null)
        {
            if (!IsConfigured) throw new InvalidOperationException("Key Vault or AUDIT_HMAC_SECRET_NAME/AUDIT_ENC_SECRET_NAME is not configured.");
            var client = _client!;
            var value = Convert.ToBase64String(key);
            var secret = new KeyVaultSecret(_encSecretName!, value);
            if (!string.IsNullOrWhiteSpace(versionTag))
            {
                secret.Properties.Tags["keyVersion"] = versionTag;
            }

            var resp = await client.SetSecretAsync(secret);
            return resp.Value.Properties.Version;
        }

        public async Task<string?> GetLatestHmacKeyVersionAsync()
        {
            if (!IsConfigured) return null;
            var client = _client!;
            try
            {
                var resp = await client.GetSecretAsync(_hmacSecretName!);
                return resp.Value.Properties.Version;
            }
            catch (RequestFailedException)
            {
                return null;
            }
        }

        public async Task<string?> GetLatestEncryptionKeyVersionAsync()
        {
            if (!IsConfigured) return null;
            var client = _client!;
            try
            {
                var resp = await client.GetSecretAsync(_encSecretName!);
                return resp.Value.Properties.Version;
            }
            catch (RequestFailedException)
            {
                return null;
            }
        }

        /// <summary>
        /// Confirms that the active versions can be retrieved explicitly. It never returns key material.
        /// </summary>
        public async Task<KeyVaultVersionValidation> ValidateActiveVersionsAsync()
        {
            if (!IsConfigured)
            {
                return new KeyVaultVersionValidation();
            }

            var hmacVersion = await GetLatestHmacKeyVersionAsync();
            var encryptionVersion = await GetLatestEncryptionKeyVersionAsync();
            var hmacKey = string.IsNullOrWhiteSpace(hmacVersion) ? null : await GetHmacKeyAsync(hmacVersion);
            var encryptionKey = string.IsNullOrWhiteSpace(encryptionVersion) ? null : await GetEncryptionKeyAsync(encryptionVersion);
            var hmacAccessible = hmacKey is { Length: > 0 };
            var encryptionAccessible = encryptionKey is { Length: > 0 };

            return new KeyVaultVersionValidation
            {
                IsConfigured = true,
                HmacVersion = hmacVersion,
                EncryptionVersion = encryptionVersion,
                HmacVersionAccessible = hmacAccessible,
                EncryptionVersionAccessible = encryptionAccessible
            };
        }
    }

    public sealed class KeyVaultVersionValidation
    {
        public bool IsConfigured { get; init; }
        public string? HmacVersion { get; init; }
        public string? EncryptionVersion { get; init; }
        public bool HmacVersionAccessible { get; init; }
        public bool EncryptionVersionAccessible { get; init; }
        public bool IsSuccessful => IsConfigured && HmacVersionAccessible && EncryptionVersionAccessible;
    }
}
