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

                // Do not set AUDIT_ENC_KEY by default in tests to avoid forcing encryption of Detalles;
                // individual tests may set it explicitly when they need to validate encryption behavior.

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
