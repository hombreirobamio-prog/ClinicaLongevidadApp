namespace ClinicaLongevidadApp.Services.KeyRotation
{
    using System;
    using System.Threading.Tasks;
    using System.Security.Cryptography;

    /// <summary>
    /// Scaffold service for key rotation workflows.
    /// Integrate with the existing IKeyProvider, key storage and audit service in subsequent changes.
    /// This file intentionally contains minimal implementation so it can be extended safely.
    /// </summary>
    public class KeyRotationService
    {
        private readonly KeyVaultKeyProvider _kvProvider = new KeyVaultKeyProvider();

        /// <summary>
        /// Return the currently active HMAC key version identifier.
        /// </summary>
        public async Task<string?> GetCurrentHmacKeyVersionAsync()
        {
            if (_kvProvider.IsConfigured)
            {
                return await _kvProvider.GetLatestHmacKeyVersionAsync();
            }
            return null;
        }

        /// <summary>
        /// Preview plan for rotating the HMAC key.
        /// Returns a RotationPlan describing the changes that would be applied.
        /// </summary>
        public async Task<RotationPlan> PreviewRotateHmacKeyAsync(string newVersion)
        {
            var oldVersion = _kvProvider.IsConfigured ? await _kvProvider.GetLatestHmacKeyVersionAsync() : null;

            // TODO: implement discovery of affected rows, estimate re-encryption/backfill cost
            var plan = new RotationPlan
            {
                KeyType = RotationKeyType.Hmac,
                OldVersion = oldVersion,
                NewVersion = newVersion,
                AffectedRowCountEstimate = 0,
                Notes = "Preview only: no changes applied. Implement discovery logic to populate AffectedRowCountEstimate."
            };

            return plan;
        }

        /// <summary>
        /// Apply rotation for HMAC key. This method currently persists the new key to Key Vault when configured and returns a plan.
        /// It does not perform re-encryption/backfill of existing rows — that must be implemented separately.
        /// </summary>
        public async Task<RotationPlan> ApplyRotateHmacKeyAsync(byte[] newHmacKey, string newVersion)
        {
            var oldVersion = _kvProvider.IsConfigured ? await _kvProvider.GetLatestHmacKeyVersionAsync() : null;
            string? createdVersion = null;

            if (_kvProvider.IsConfigured)
            {
                // Persist new key material to Key Vault and record the resulting secret version.
                createdVersion = await _kvProvider.SetHmacKeyAsync(newHmacKey, newVersion);
            }
            else
            {
                throw new InvalidOperationException("Key Vault or the configured audit secret names are unavailable. Aborting apply to avoid storing keys in a different location.");
            }

            var plan = new RotationPlan
            {
                KeyType = RotationKeyType.Hmac,
                OldVersion = oldVersion,
                NewVersion = createdVersion ?? newVersion,
                AffectedRowCountEstimate = 0,
                Notes = "Apply persisted new HMAC key in Key Vault. Backfill/re-encryption not performed by this scaffold."
            };

            return plan;
        }

        /// <summary>
        /// Rotate the encryption key (AES-GCM) to a new key/version.
        /// Implement safe re-encryption/backfill procedures when required.
        /// </summary>
        public async Task<RotationPlan> ApplyRotateEncryptionKeyAsync(byte[] newEncKey, string newVersion)
        {
            var oldVersion = _kvProvider.IsConfigured ? await _kvProvider.GetLatestEncryptionKeyVersionAsync() : null;
            string? createdVersion = null;

            if (_kvProvider.IsConfigured)
            {
                createdVersion = await _kvProvider.SetEncryptionKeyAsync(newEncKey, newVersion);
            }
            else
            {
                throw new InvalidOperationException("Key Vault or the configured audit secret names are unavailable. Aborting apply to avoid storing keys in a different location.");
            }

            var plan = new RotationPlan
            {
                KeyType = RotationKeyType.Encryption,
                OldVersion = oldVersion,
                NewVersion = createdVersion ?? newVersion,
                AffectedRowCountEstimate = 0,
                Notes = "Apply persisted new encryption key in Key Vault. Backfill/re-encryption not performed by this scaffold."
            };

            return plan;
        }

        /// <summary>
        /// Preview plan for rotating the encryption key.
        /// </summary>
        public async Task<RotationPlan> PreviewRotateEncryptionKeyAsync(string newVersion)
        {
            var oldVersion = _kvProvider.IsConfigured ? await _kvProvider.GetLatestEncryptionKeyVersionAsync() : null;

            var plan = new RotationPlan
            {
                KeyType = RotationKeyType.Encryption,
                OldVersion = oldVersion,
                NewVersion = newVersion,
                AffectedRowCountEstimate = 0,
                Notes = "Preview only: no changes applied. Implement discovery logic to populate AffectedRowCountEstimate."
            };

            return plan;
        }

        // Test/helper utilities
        public static byte[] GenerateRandomKey(int size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            var b = new byte[size];
            RandomNumberGenerator.Fill(b);
            return b;
        }

        public static string GenerateRandomKeyBase64(int size)
        {
            return Convert.ToBase64String(GenerateRandomKey(size));
        }
    }

    public enum RotationKeyType
    {
        Hmac,
        Encryption
    }

    public class RotationPlan
    {
        public RotationKeyType KeyType { get; set; }

        public string? OldVersion { get; set; }

        public string? NewVersion { get; set; }

        public long AffectedRowCountEstimate { get; set; }

        public string? Notes { get; set; }
    }
}
