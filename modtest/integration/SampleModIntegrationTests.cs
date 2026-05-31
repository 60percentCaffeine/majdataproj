using System;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class SampleModIntegrationTests
    {
        [Fact]
        public async Task SampleModCoreCanBeObservedThroughBridgeEval()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                BridgeResponse response = await run.Client.EvalIsolatedAsync("new TestMod.Core.TestModLogic().StartupMessage()");

                Assert.Equal(200, response.StatusCode);
                Assert.Contains("\"ok\":true", response.Content);
                Assert.Contains("\"Loaded\"", response.Content);
            }
            finally
            {
                if (run != null)
                {
                    await run.ShutdownOrKillAsync();
                    run.Dispose();
                }

                harness.CollectLogs("integration");
            }
        }
    }
}
