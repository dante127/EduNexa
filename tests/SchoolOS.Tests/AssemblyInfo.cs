using Xunit;

// Integration-test fixtures configure process-wide environment variables before building
// the app host (startup-time configuration reads do not see WebApplicationFactory's
// ConfigureAppConfiguration). Running test classes sequentially prevents overlapping
// fixtures from racing on that shared state (observed: cross-container migration errors).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
