using Xunit;

// Disable parallel test execution to avoid interference on environment variables and
// shared resources used by integration-style tests (e.g., audit DB, HMAC keys).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
