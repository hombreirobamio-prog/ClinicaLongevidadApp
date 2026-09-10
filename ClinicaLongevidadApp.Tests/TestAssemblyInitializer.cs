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
                    // Use a 32-byte key (AES-256) encoded as base64 for tests to ensure AES-GCM encryption/decryption works.
                    var key32 = "0123456789ABCDEF0123456789ABCDEF"; // 32 chars => 32 bytes in UTF8
                    var enc = Convert.ToBase64String(Encoding.UTF8.GetBytes(key32));
                    Environment.SetEnvironmentVariable(encEnv, enc);
                }

                // Enable authorization enforcement for tests by default (opt-in for app runtime)
                const string authEnv = "AUDIT_ENFORCE_AUTH";
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(authEnv)))
                {
                    Environment.SetEnvironmentVariable(authEnv, "1");
                }
            }
            catch
            {
                // Swallow any errors to avoid breaking test discovery
            }
        }
    }
}
