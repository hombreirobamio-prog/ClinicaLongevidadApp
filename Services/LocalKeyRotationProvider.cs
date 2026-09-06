using System;
using System.IO;

namespace ClinicaLongevidadApp.Services
{
    /// <summary>
    /// Local rotation provider: persists keys to AppData and sets environment variables for the current process.
    /// Intended for development/testing only.
    /// </summary>
    public class LocalKeyRotationProvider : IKeyRotationProvider
    {
        private readonly string _folder;

        public LocalKeyRotationProvider()
        {
            _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicaLongevidadApp", "keys");
            Directory.CreateDirectory(_folder);
        }

        public void PersistHmacKey(byte[] key)
        {
            string path = Path.Combine(_folder, "hmac.key");
            File.WriteAllText(path, Convert.ToBase64String(key));
            // set for current process
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", Convert.ToBase64String(key));
        }

        public void PersistEncryptionKey(byte[] key)
        {
            string path = Path.Combine(_folder, "enc.key");
            File.WriteAllText(path, Convert.ToBase64String(key));
            Environment.SetEnvironmentVariable("AUDIT_ENC_KEY", Convert.ToBase64String(key));
        }
    }
}
