using System;
using System.Threading.Tasks;
using ClinicaLongevidadApp.Services;

namespace ClinicaLongevidadApp.Services.KeyRotation
{
    /// <summary>
    /// Factory/resolver that returns an executor able to apply key rotations using the best available provider
    /// (Key Vault when configured, otherwise the local development provider). This centralizes the environment
    /// checks and allows callers (UI) to remain simple.
    /// </summary>
    public static class KeyRotationProviderFactory
    {
        public interface IRotationExecutor
        {
            Task<RotationPlan> ApplyRotateHmacAsync(byte[] newHmacKey, string newVersion);
            Task<RotationPlan> ApplyRotateEncAsync(byte[] newEncKey, string newVersion);
        }

        public static IRotationExecutor Create(AuditoriaService? auditoriaService = null)
        {
            var kv = Environment.GetEnvironmentVariable("KEYVAULT_URI");
            var require = (Environment.GetEnvironmentVariable("REQUIRE_KEYVAULT") ?? string.Empty).Equals("true", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(kv))
            {
                return new KeyVaultExecutor();
            }

            if (require)
            {
                throw new InvalidOperationException("Key Vault is required by configuration (REQUIRE_KEYVAULT=true) but KEYVAULT_URI is not set.");
            }

            return new LocalExecutor(auditoriaService);
        }

        private class KeyVaultExecutor : IRotationExecutor
        {
            private readonly KeyRotationService _svc = new KeyRotationService();

            public async Task<RotationPlan> ApplyRotateHmacAsync(byte[] newHmacKey, string newVersion)
            {
                return await _svc.ApplyRotateHmacKeyAsync(newHmacKey, newVersion);
            }

            public async Task<RotationPlan> ApplyRotateEncAsync(byte[] newEncKey, string newVersion)
            {
                return await _svc.ApplyRotateEncryptionKeyAsync(newEncKey, newVersion);
            }
        }

        private class LocalExecutor : IRotationExecutor
        {
            private readonly AuditoriaService? _auditoriaService;

            public LocalExecutor(AuditoriaService? auditoriaService)
            {
                _auditoriaService = auditoriaService;
            }

            public Task<RotationPlan> ApplyRotateHmacAsync(byte[] newHmacKey, string newVersion)
            {
                // Use the LocalKeyRotationProvider to persist key and produce a RotationPlan compatible with KeyVault path
                var provider = new LocalKeyRotationProvider();
                provider.PersistHmacKey(newHmacKey);

                var plan = new RotationPlan
                {
                    KeyType = RotationKeyType.Hmac,
                    OldVersion = Environment.GetEnvironmentVariable("AUDIT_HMAC_KEY_VERSION"),
                    NewVersion = Environment.GetEnvironmentVariable("AUDIT_HMAC_KEY_VERSION") ?? newVersion,
                    AffectedRowCountEstimate = 0,
                    Notes = "Applied local rotation: key persisted to AppData (development fallback)."
                };

                // no audit here: auditing is performed by the application layer (VM) to avoid duplicate entries

                return Task.FromResult(plan);
            }

            public Task<RotationPlan> ApplyRotateEncAsync(byte[] newEncKey, string newVersion)
            {
                var provider = new LocalKeyRotationProvider();
                provider.PersistEncryptionKey(newEncKey);

                var plan = new RotationPlan
                {
                    KeyType = RotationKeyType.Encryption,
                    OldVersion = Environment.GetEnvironmentVariable("AUDIT_ENC_KEY_VERSION"),
                    NewVersion = Environment.GetEnvironmentVariable("AUDIT_ENC_KEY_VERSION") ?? newVersion,
                    AffectedRowCountEstimate = 0,
                    Notes = "Applied local rotation: encryption key persisted to AppData (development fallback)."
                };

                // no audit here: auditing is performed by the application layer (VM) to avoid duplicate entries

                return Task.FromResult(plan);
            }
        }
    }
}
