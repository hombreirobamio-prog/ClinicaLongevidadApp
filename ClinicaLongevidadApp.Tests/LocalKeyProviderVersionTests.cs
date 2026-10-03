using ClinicaLongevidadApp.Services;
using Xunit;

namespace ClinicaLongevidadApp.Tests;

public class LocalKeyProviderVersionTests
{
    [Fact]
    public void ExplicitVersionCannotFallbackToCurrentKey()
    {
        var names = new[] { "AUDIT_HMAC_KEY", "AUDIT_HMAC_KEY_VERSION", "AUDIT_ENC_KEY", "AUDIT_ENC_KEY_VERSION" };
        var previous = names.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            Environment.SetEnvironmentVariable(names[0], Convert.ToBase64String(new byte[32]));
            Environment.SetEnvironmentVariable(names[1], "v2");
            Environment.SetEnvironmentVariable(names[2], Convert.ToBase64String(new byte[32]));
            Environment.SetEnvironmentVariable(names[3], "v2");
            var provider = new LocalKeyProvider();
            foreach (var version in new[] { "local", "v1", "V2" })
            {
                Assert.Null(provider.GetHmacKeyByVersion(version));
                Assert.Null(provider.GetEncryptionKeyByVersion(version));
            }
            Assert.NotNull(provider.GetHmacKeyByVersion("v2"));
            Assert.NotNull(provider.GetEncryptionKeyByVersion("v2"));
        }
        finally
        {
            for (var i = 0; i < names.Length; i++) Environment.SetEnvironmentVariable(names[i], previous[i]);
        }
    }
}
