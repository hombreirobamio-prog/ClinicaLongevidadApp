using ClinicaLongevidadApp.Services;
using System.Collections.Generic;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class CacheServiceTests
    {
        [Fact]
        public void GetOrSet_ShouldReturnCachedValueAndInvalidate()
        {
            // Arrange
            CacheService.Clear();

            var data = new List<int> { 1, 2, 3 };

            // Act
            CacheService.Set("test_key", data, durationMinutes: 1);
            var cached = CacheService.Get<List<int>>("test_key");

            // Assert
            Assert.NotNull(cached);
            Assert.Contains(2, cached);

            // Invalidate and ensure null
            CacheService.Invalidate("test_key");
            var after = CacheService.Get<List<int>>("test_key");
            Assert.Null(after);
        }
    }
}
