using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class StructuredEvalSerializationRegressionTests
    {
        [Fact]
        public async Task EvalReturnsStructuredPrimitiveArrayDictionaryAndDtoShapes()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                BridgeResponse response = await run.Client.EvalIsolatedAsync(@"new {
    Primitive = 123,
    Array = new[] { 2, 4, 6 },
    Dictionary = new System.Collections.Generic.Dictionary<string, int> {
        { ""alpha"", 7 },
        { ""beta"", 9 }
    },
    Dto = new {
        Name = ""structured-dto"",
        Score = 42,
        Active = true
    }
}");

                Assert.Equal(200, response.StatusCode);
                JsonElement root = JsonDocument.Parse(response.Content).RootElement.Clone();
                Assert.True(root.GetProperty("ok").GetBoolean(), response.Content);
                Assert.Equal("execution", root.GetProperty("phase").GetString());

                JsonElement properties = root.GetProperty("result").GetProperty("properties");
                Assert.Equal(123, properties.GetProperty("Primitive").GetInt32());

                JsonElement array = properties.GetProperty("Array");
                Assert.NotEqual(JsonValueKind.String, array.ValueKind);
                Assert.Equal(3, array.GetProperty("items").GetArrayLength());
                Assert.Equal(2, array.GetProperty("items")[0].GetInt32());
                Assert.Equal(4, array.GetProperty("items")[1].GetInt32());
                Assert.Equal(6, array.GetProperty("items")[2].GetInt32());

                JsonElement dictionary = properties.GetProperty("Dictionary");
                Assert.NotEqual(JsonValueKind.String, dictionary.ValueKind);
                JsonElement entries = dictionary.GetProperty("entries");
                Assert.Equal(2, entries.GetArrayLength());
                AssertDictionaryEntry(entries, "alpha", 7);
                AssertDictionaryEntry(entries, "beta", 9);

                JsonElement dto = properties.GetProperty("Dto");
                Assert.NotEqual(JsonValueKind.String, dto.ValueKind);
                JsonElement dtoProperties = dto.GetProperty("properties");
                Assert.Equal("structured-dto", dtoProperties.GetProperty("Name").GetString());
                Assert.Equal(42, dtoProperties.GetProperty("Score").GetInt32());
                Assert.True(dtoProperties.GetProperty("Active").GetBoolean());
            }
            finally
            {
                if (run != null)
                {
                    await run.ShutdownOrKillAsync();
                    run.Dispose();
                }

                harness.CollectLogs("structured-eval-serialization");
            }
        }

        private static void AssertDictionaryEntry(JsonElement entries, string key, int value)
        {
            for (int i = 0; i < entries.GetArrayLength(); i++)
            {
                JsonElement entry = entries[i];
                if (entry.GetProperty("key").GetString() == key)
                {
                    Assert.Equal(value, entry.GetProperty("value").GetInt32());
                    return;
                }
            }

            throw new Xunit.Sdk.XunitException("Missing dictionary entry for key " + key + ".");
        }
    }
}
