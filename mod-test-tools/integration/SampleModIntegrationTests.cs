using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                BridgeResponse response = await run.Client.EvalIsolatedAsync(@"
new {
    SampleModAssemblyLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == ""TestMod""),
    CoreAssemblyLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == ""TestMod.Core""),
    SampleModTypeFound = Type.GetType(""TestMod.TestMod, TestMod"") != null,
    CoreTypeFound = Type.GetType(""TestMod.Core.TestModLogic, TestMod.Core"") != null,
    StartupMessage = new TestMod.Core.TestModLogic().StartupMessage(),
    AddedScore = new TestMod.Core.TestModLogic().AddScore(100, 25),
    NegativeScoreIgnored = new TestMod.Core.TestModLogic().AddScore(100, -5)
}");

                JsonElement properties = EvalResultProperties(response);
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

            Exception logFailure = Record.Exception(() => AssertNoSampleModStartupFailures(Path.Combine(harness.ArtifactsRoot, "integration")));
            if (scenarioFailure != null && logFailure != null)
            {
                throw new AggregateException(scenarioFailure, logFailure);
            }

            if (scenarioFailure != null)
            {
                throw scenarioFailure;
            }

            if (logFailure != null)
            {
                throw logFailure;
            }
        }

        private static JsonElement EvalResultProperties(BridgeResponse response)
        {
            Assert.Equal(200, response.StatusCode);
            using JsonDocument document = JsonDocument.Parse(response.Content);
            Assert.True(document.RootElement.GetProperty("ok").GetBoolean(), response.Content);
            Assert.Equal("execution", document.RootElement.GetProperty("phase").GetString());
            JsonElement result = document.RootElement.GetProperty("result");
            return result.GetProperty("properties").Clone();
        }

        private static void AssertNoSampleModStartupFailures(string artifactDirectory)
        {
            Assert.True(Directory.Exists(artifactDirectory), "Missing integration log artifact directory: " + artifactDirectory);

            string[] logFiles = Directory.GetFiles(artifactDirectory, "*.log", SearchOption.AllDirectories);
            Assert.NotEmpty(logFiles);

            string[] failureNeedles =
            {
                "TestMod failed",
                "TestMod.Core failed",
                "Unhandled Exception",
                "Unhandled exception",
                "NullReferenceException",
                "TypeLoadException",
                "FileNotFoundException",
                "MissingMethodException"
            };

            List<string> hits = new List<string>();
            foreach (string logFile in logFiles)
            {
                foreach (string line in File.ReadLines(logFile))
                {
                    if (failureNeedles.Any(line.Contains))
                    {
                        hits.Add(Path.GetFileName(logFile) + ": " + line);
                    }
                }
            }

            Assert.True(hits.Count == 0, "Unexpected sample mod startup failure log lines:\n" + string.Join("\n", hits));
        }
    }
}
