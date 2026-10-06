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
            _folder = Path.Combine(ClinicaLongevidadApp.Services.AppPaths.KeysDir);
            try { Directory.CreateDirectory(_folder); } catch { }
        }

        public void PersistHmacKey(byte[] key)
        {
            string path = Path.Combine(_folder, "hmac.key");
            File.WriteAllText(path, Convert.ToBase64String(key));
            var b64 = Convert.ToBase64String(key);
            // persist key to file and set environment variables for current process and user for convenience in dev
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", b64, EnvironmentVariableTarget.Process);
            try { Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", b64, EnvironmentVariableTarget.User); } catch { }

            // generate and persist a key version identifier
            var version = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var verPath = Path.Combine(_folder, "hmac.key.version");
            try { File.WriteAllText(verPath, version); } catch { }
            try { Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY_VERSION", version, EnvironmentVariableTarget.Process); } catch { }
            try { Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY_VERSION", version, EnvironmentVariableTarget.User); } catch { }
        }

        public void PersistEncryptionKey(byte[] key)
        {
            string path = Path.Combine(_folder, "enc.key");
            File.WriteAllText(path, Convert.ToBase64String(key));
            var b64 = Convert.ToBase64String(key);
            Environment.SetEnvironmentVariable("AUDIT_ENC_KEY", b64, EnvironmentVariableTarget.Process);
            try { Environment.SetEnvironmentVariable("AUDIT_ENC_KEY", b64, EnvironmentVariableTarget.User); } catch { }

            var version = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var verPath = Path.Combine(_folder, "enc.key.version");
            try { File.WriteAllText(verPath, version); } catch { }
            try { Environment.SetEnvironmentVariable("AUDIT_ENC_KEY_VERSION", version, EnvironmentVariableTarget.Process); } catch { }
            try { Environment.SetEnvironmentVariable("AUDIT_ENC_KEY_VERSION", version, EnvironmentVariableTarget.User); } catch { }
        }
    }
}
