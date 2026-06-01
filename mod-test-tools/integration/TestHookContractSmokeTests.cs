using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class TestHookContractSmokeTests
    {
        [Fact]
        public async Task ReadinessFileAndHealthEndpointExposeMatchingHookContract()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));

                Assert.True(File.Exists(harness.ReadyPath), "Test hook readiness file should exist at " + harness.ReadyPath);
                JsonElement readiness = JsonDocument.Parse(File.ReadAllText(harness.ReadyPath)).RootElement.Clone();
                BridgeResponse healthResponse = await run.Client.HealthAsync();
                Assert.Equal(200, healthResponse.StatusCode);
                JsonElement health = JsonDocument.Parse(healthResponse.Content).RootElement.Clone();

                Assert.True(readiness.GetProperty("pid").GetInt32() > 0, "Readiness pid should be a positive process id.");
                Assert.False(string.IsNullOrWhiteSpace(readiness.GetProperty("host").GetString()));
                Assert.True(readiness.GetProperty("port").GetInt32() > 0, "Readiness port should be a positive TCP port.");
                Assert.True(DateTimeOffset.TryParse(readiness.GetProperty("startedAt").GetString(), out _), "Readiness startedAt should parse as a timestamp.");
                Assert.False(string.IsNullOrWhiteSpace(readiness.GetProperty("bridgeVersion").GetString()));

                Assert.True(health.GetProperty("ok").GetBoolean(), "/health should report ok:true.");
                Assert.Equal(readiness.GetProperty("pid").GetInt32(), health.GetProperty("pid").GetInt32());
                Assert.Equal(readiness.GetProperty("host").GetString(), health.GetProperty("host").GetString());
                Assert.Equal(readiness.GetProperty("port").GetInt32(), health.GetProperty("port").GetInt32());
                Assert.Equal(readiness.GetProperty("bridgeVersion").GetString(), health.GetProperty("bridgeVersion").GetString());
                Assert.Equal(readiness.GetProperty("replEnabled").GetBoolean(), health.GetProperty("replEnabled").GetBoolean());
                Assert.True(health.GetProperty("mainThreadDispatcherReady").GetBoolean(), "Main-thread dispatcher should be ready by the time /health passes.");
            }
            finally
            {
                if (run != null)
                {
                    await run.ShutdownOrKillAsync();
                    run.Dispose();
                }

                harness.CollectLogs("test-hook-contract");
            }
        }
    }
}
