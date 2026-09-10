using System;
using System.Threading.Tasks;
using ClinicaLongevidadApp.Services.KeyRotation;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class KeyRotationTests
    {
        [Fact]
        public void GenerateRandomKey_Base64_and_size()
        {
            var raw = KeyRotationService.GenerateRandomKey(32);
            Assert.NotNull(raw);
            Assert.Equal(32, raw.Length);

            var b64 = KeyRotationService.GenerateRandomKeyBase64(32);
            Assert.False(string.IsNullOrWhiteSpace(b64));
            var decoded = Convert.FromBase64String(b64);
            Assert.Equal(32, decoded.Length);
        }

        [Fact]
        public async Task PreviewRotateHmacKey_ReturnsPlan()
        {
            var svc = new KeyRotationService();
            var plan = await svc.PreviewRotateHmacKeyAsync("v-test");
            Assert.NotNull(plan);
            Assert.Equal(RotationKeyType.Hmac, plan.KeyType);
            Assert.Equal("v-test", plan.NewVersion);
            Assert.True(plan.AffectedRowCountEstimate >= 0);
        }

        [Fact]
        public async Task PreviewRotateEncryptionKey_ReturnsPlan()
        {
            var svc = new KeyRotationService();
            var plan = await svc.PreviewRotateEncryptionKeyAsync("v-test-enc");
            Assert.NotNull(plan);
            Assert.Equal(RotationKeyType.Encryption, plan.KeyType);
            Assert.Equal("v-test-enc", plan.NewVersion);
            Assert.True(plan.AffectedRowCountEstimate >= 0);
        }
    }
}
