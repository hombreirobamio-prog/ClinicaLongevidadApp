using System;

namespace ClinicaLongevidadApp.Services
{
    public interface IKeyProvider
    {
        /// <summary>
        /// Returns HMAC key bytes used to sign audit events.
        /// </summary>
        byte[]? GetHmacKey();

        /// <summary>
        /// Returns encryption key bytes used to encrypt sensitive fields (AES-256).
        /// </summary>
        byte[]? GetEncryptionKey();

        /// <summary>
        /// Optional: returns a stable identifier/version for the HMAC key (e.g. secret version or key id).
        /// </summary>
        string? GetHmacKeyVersion();

        /// <summary>
        /// Optional: returns a stable identifier/version for the encryption key.
        /// </summary>
        string? GetEncryptionKeyVersion();

        /// <summary>
        /// Retrieve a historical HMAC key by its version identifier. Return null if not available.
        /// </summary>
        byte[]? GetHmacKeyByVersion(string? version);

        /// <summary>
        /// Retrieve a historical encryption key by its version identifier. Return null if not available.
        /// </summary>
        byte[]? GetEncryptionKeyByVersion(string? version);
    }
}
