using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    internal static class IntegrationAssertions
    {
        public static JsonElement EvalResultProperties(BridgeResponse response)
        {
            Assert.Equal(200, response.StatusCode);
            using JsonDocument document = JsonDocument.Parse(response.Content);
            Assert.True(document.RootElement.GetProperty("ok").GetBoolean(), response.Content);
            Assert.Equal("execution", document.RootElement.GetProperty("phase").GetString());
            JsonElement result = document.RootElement.GetProperty("result");
            return result.GetProperty("properties").Clone();
        }

        public static void ThrowCombined(Exception scenarioFailure, Exception artifactFailure)
        {
            if (scenarioFailure != null && artifactFailure != null)
            {
                throw new AggregateException(scenarioFailure, artifactFailure);
            }

            if (scenarioFailure != null)
            {
                throw scenarioFailure;
            }

            if (artifactFailure != null)
            {
                throw artifactFailure;
            }
        }

        public static void AssertNoSampleModStartupFailures(string artifactDirectory)
        {
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

            AssertNoMatchingLogLines(artifactDirectory, failureNeedles, "Unexpected sample mod startup failure log lines");
        }

        public static void AssertNoFatalBootFailures(string artifactDirectory)
        {
            string[] failureNeedles =
            {
                "[ERROR]",
                "[FATAL]",
                "Unhandled Exception",
                "Unhandled exception",
                "Fatal error",
                "fatal error"
            };

            AssertNoMatchingLogLines(artifactDirectory, failureNeedles, "Unexpected fatal boot log lines");
        }

        private static void AssertNoMatchingLogLines(string artifactDirectory, string[] needles, string message)
        {
            Assert.True(Directory.Exists(artifactDirectory), "Missing integration log artifact directory: " + artifactDirectory);

            string[] logFiles = Directory.GetFiles(artifactDirectory, "*.log", SearchOption.AllDirectories);
            Assert.NotEmpty(logFiles);

            List<string> hits = new List<string>();
            foreach (string logFile in logFiles)
            {
                foreach (string line in File.ReadLines(logFile))
                {
                    if (needles.Any(line.Contains))
                    {
                        hits.Add(Path.GetFileName(logFile) + ": " + line);
                    }
                }
            }

            Assert.True(hits.Count == 0, message + ":\n" + string.Join("\n", hits));
        }
    }
}
