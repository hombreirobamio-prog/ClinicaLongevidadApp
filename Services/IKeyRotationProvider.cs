using System;

namespace ClinicaLongevidadApp.Services
{
    public interface IKeyRotationProvider
    {
        /// <summary>
        /// Persist new HMAC key (base64 or raw bytes).
        /// </summary>
        void PersistHmacKey(byte[] key);

        /// <summary>
        /// Persist new encryption key.
        /// </summary>
        void PersistEncryptionKey(byte[] key);
    }
}
