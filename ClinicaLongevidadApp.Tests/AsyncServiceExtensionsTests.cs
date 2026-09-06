using ClinicaLongevidadApp.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class AsyncServiceExtensionsTests
    {
        [Fact]
        public async Task ExecuteWithRetryAsync_SucceedsAfterRetries()
        {
            int attempts = 0;

            async Task<string> Operation()
            {
                attempts++;
                if (attempts < 3)
                {
                    await Task.Delay(10);
                    throw new InvalidOperationException("Transient");
                }

                await Task.Delay(10);
                return "ok";
            }

            string result = await AsyncServiceExtensions.ExecuteWithRetryAsync(
                () => Operation(),
                "TestOperation",
                maxRetries: 3,
                delayMilliseconds: 10);

            Assert.Equal("ok", result);
            Assert.Equal(3, attempts);
        }

        [Fact]
        public async Task ExecuteWithTimeoutAsync_ThrowsTimeoutException()
        {
            async Task<int> SlowOp()
            {
                await Task.Delay(2000);
                return 42;
            }

            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await AsyncServiceExtensions.ExecuteWithTimeoutAsync(
                    () => SlowOp(),
                    "TimeoutOp",
                    TimeSpan.FromMilliseconds(100));
            });
        }
    }
}
