using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class SerializationLimitsAndCyclesRegressionTests
    {
        [Fact]
        public async Task EvalSerializationHandlesCyclesAndReportsBoundedLimitFailures()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));

                BridgeResponse cycleResponse = await run.Client.EvalIsolatedAsync(@"new Func<object>(() => {
    var node = new System.Collections.Generic.Dictionary<string, object>();
    node[""name""] = ""cycle-root"";
    node[""self""] = node;
    return node;
})()");
                Assert.Equal(200, cycleResponse.StatusCode);
                JsonElement cycleRoot = JsonDocument.Parse(cycleResponse.Content).RootElement.Clone();
                Assert.True(cycleRoot.GetProperty("ok").GetBoolean(), cycleResponse.Content);
                JsonElement cycleResult = cycleRoot.GetProperty("result");
                Assert.True(cycleResult.GetProperty("$id").GetInt32() > 0, "Cycle root should carry a serializer id.");
                JsonElement entries = cycleResult.GetProperty("entries");
                JsonElement selfEntry = FindDictionaryEntry(entries, "self");
                Assert.Equal(cycleResult.GetProperty("$id").GetInt32(), selfEntry.GetProperty("value").GetProperty("$ref").GetInt32());

                BridgeResponse depthResponse = await run.Client.EvalIsolatedAsync(new EvalRequest
                {
                    Code = "new { Inner = new { Value = 7 } }",
                    MaxDepth = 1,
                    MaxResponseBytes = 262144
                });
                AssertSerializationFailure(depthResponse, "maxDepth");

                BridgeResponse sizeResponse = await run.Client.EvalIsolatedAsync(new EvalRequest
                {
                    Code = "new string('x', 4096)",
                    MaxDepth = 6,
                    MaxResponseBytes = 80
                });
                AssertSerializationFailure(sizeResponse, "maxResponseBytes");
            }
            finally
            {
                if (run != null)
                {
                    await run.ShutdownOrKillAsync();
                    run.Dispose();
                }

                harness.CollectLogs("serialization-limits-cycles");
            }
        }

        private static JsonElement FindDictionaryEntry(JsonElement entries, string key)
        {
            for (int i = 0; i < entries.GetArrayLength(); i++)
            {
                JsonElement entry = entries[i];
                if (entry.GetProperty("key").GetString() == key)
                {
                    return entry;
                }
            }

            throw new Xunit.Sdk.XunitException("Missing dictionary entry for key " + key + ".");
        }

        private static void AssertSerializationFailure(BridgeResponse response, string scenario)
        {
            Assert.True(response.StatusCode >= 400, "Expected serialization failure for " + scenario + ":\n" + response.Content);
            JsonElement root = JsonDocument.Parse(response.Content).RootElement.Clone();
            Assert.False(root.GetProperty("ok").GetBoolean(), response.Content);
            Assert.Equal("serialization", root.GetProperty("phase").GetString());
            Assert.True(root.TryGetProperty("error", out JsonElement error), "Serialization failure should include an error object:\n" + response.Content);
            Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("type").GetString()));
        }
    }
}
