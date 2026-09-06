using System;
using System.Text;
using System.Runtime.CompilerServices;

namespace ClinicaLongevidadApp.Tests
{
    // Assembly initializer to ensure required environment variables are present
    // for tests that depend on HMAC/encryption keys. This runs once before any
    // tests in the assembly (C# Module Initializer).
    internal static class TestAssemblyInitializer
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            try
            {
                // Only set defaults if not already provided by CI or environment
                const string hmacEnv = "AUDIT_HMAC_KEY";
                const string encEnv = "AUDIT_ENC_KEY";

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(hmacEnv)))
                {
                    Environment.SetEnvironmentVariable(hmacEnv, "test-key-0123456789");
                }

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(encEnv)))
                {
                    var enc = Convert.ToBase64String(Encoding.UTF8.GetBytes("encryptionkey1234567890123456"));
                    Environment.SetEnvironmentVariable(encEnv, enc);
                }
            }
            catch
            {
                // Swallow any errors to avoid breaking test discovery
            }
        }
    }
}
