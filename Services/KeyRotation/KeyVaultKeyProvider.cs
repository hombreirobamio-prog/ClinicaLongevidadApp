namespace ClinicaLongevidadApp.Services.KeyRotation
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal scaffold for a Key Vault backed key provider.
    /// Implement actual Azure Key Vault SDK integration when ready.
    /// </summary>
    public class KeyVaultKeyProvider
    {
        private readonly string? _vaultUri;

        public KeyVaultKeyProvider()
        {
            _vaultUri = Environment.GetEnvironmentVariable("KEYVAULT_URI");
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_vaultUri);

        public Task<byte[]?> GetHmacKeyAsync(string version)
        {
            // TODO: connect to Key Vault and retrieve the secret/key for the given version.
            return Task.FromResult<byte[]?>(null);
        }

        public Task<byte[]?> GetEncryptionKeyAsync(string version)
        {
            // TODO: connect to Key Vault and retrieve the encryption key for the given version.
            return Task.FromResult<byte[]?>(null);
        }

        public Task<string?> GetLatestHmacKeyVersionAsync()
        {
            // TODO: query Key Vault for latest HMAC key version
            return Task.FromResult<string?>(null);
        }

        public Task<string?> GetLatestEncryptionKeyVersionAsync()
        {
            // TODO: query Key Vault for latest encryption key version
            return Task.FromResult<string?>(null);
        }
    }
}
