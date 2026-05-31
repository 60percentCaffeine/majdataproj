using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class SampleModIntegrationTests
    {
        [Fact]
        public async Task SampleModInvariantSuitePassesInMajdataPlay()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            Exception scenarioFailure = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                BridgeResponse response = await run.Client.EvalIsolatedAsync(@"new {
    SampleModAssemblyLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == ""TestMod""),
    CoreAssemblyLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == ""TestMod.Core""),
    SampleModTypeFound = Type.GetType(""TestMod.TestMod, TestMod"") != null,
    CoreTypeFound = Type.GetType(""TestMod.Core.TestModLogic, TestMod.Core"") != null,
    StartupMessage = new TestMod.Core.TestModLogic().StartupMessage(),
    AddedScore = new TestMod.Core.TestModLogic().AddScore(100, 25),
    NegativeScoreIgnored = new TestMod.Core.TestModLogic().AddScore(100, -5)
}");

                JsonElement properties = IntegrationAssertions.EvalResultProperties(response);
                Assert.True(properties.GetProperty("SampleModAssemblyLoaded").GetBoolean());
                Assert.True(properties.GetProperty("CoreAssemblyLoaded").GetBoolean());
                Assert.True(properties.GetProperty("SampleModTypeFound").GetBoolean());
                Assert.True(properties.GetProperty("CoreTypeFound").GetBoolean());
                Assert.Equal("Loaded", properties.GetProperty("StartupMessage").GetString());
                Assert.Equal(125, properties.GetProperty("AddedScore").GetInt32());
                Assert.Equal(100, properties.GetProperty("NegativeScoreIgnored").GetInt32());
            }
            catch (Exception ex)
            {
                scenarioFailure = ex;
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

            Exception logFailure = Record.Exception(() => IntegrationAssertions.AssertNoSampleModStartupFailures(Path.Combine(harness.ArtifactsRoot, "integration")));
            IntegrationAssertions.ThrowCombined(scenarioFailure, logFailure);
        }
    }
}
