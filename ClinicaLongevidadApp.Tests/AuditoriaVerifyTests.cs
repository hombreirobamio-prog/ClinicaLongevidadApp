using System;
using System.IO;
using ClinicaLongevidadApp.Services;
using Xunit;

namespace ClinicaLongevidadApp.Tests
{
    public class AuditoriaVerifyTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _conn;

        public AuditoriaVerifyTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"auditoria_verify_test_{Guid.NewGuid():N}.db");
            _conn = $"Data Source={_dbPath}";
        }

        [Fact]
        public void VerifyIntegrity_OnEmptyDb_ReturnsNoErrors()
        {
            var svc = new AuditoriaService(_conn);
            var errors = svc.VerifyIntegrity();
            Assert.NotNull(errors);
            Assert.Empty(errors);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
