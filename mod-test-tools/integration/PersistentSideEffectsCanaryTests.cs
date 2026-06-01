using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class PersistentSideEffectsCanaryTests
    {
        private static readonly string[] MonitoredRelativePaths =
        {
            "settings.json",
            Path.Combine("Cache", "Runtime", "config.json"),
            "ChartSetting.db",
            "MajScores.db",
            "MajDatabase.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db"
        };

        [Fact]
        public async Task BootRunDoesNotMutatePersistentUserConfigOrProfileFiles()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            string gameRoot = harness.GameRoot;
            IReadOnlyDictionary<string, FileFingerprint> before = Snapshot(gameRoot);
            HarnessRun run = null;
            Exception scenarioFailure = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                await WaitForSideEffectProbeAsync(run.Client, TimeSpan.FromSeconds(75));
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

                harness.CollectLogs("persistent-side-effects");
            }

            Exception logFailure = Record.Exception(() => IntegrationAssertions.AssertNoFatalBootFailures(Path.Combine(harness.ArtifactsRoot, "persistent-side-effects")));
            IReadOnlyDictionary<string, FileFingerprint> after = Snapshot(gameRoot);
            Exception sideEffectFailure = Record.Exception(() => AssertNoPersistentDiffs(before, after));
            IntegrationAssertions.ThrowCombined(scenarioFailure, Combine(logFailure, sideEffectFailure));
        }

        private static async Task WaitForSideEffectProbeAsync(TestClient client, TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            JsonElement latest = default;
            while (DateTimeOffset.UtcNow < deadline)
            {
                BridgeResponse response = await client.EvalIsolatedAsync(@"new {
    ActiveSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
    ActiveSceneLoaded = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isLoaded,
    FrameCount = UnityEngine.Time.frameCount,
    SongStorageReady = MajdataPlay.SongStorage.Collections != null && MajdataPlay.SongStorage.Collections.Length > 0 && !MajdataPlay.SongStorage.IsEmpty
}");
                latest = IntegrationAssertions.EvalResultProperties(response);
                if (latest.GetProperty("ActiveSceneLoaded").GetBoolean()
                    && latest.GetProperty("FrameCount").GetInt32() > 0
                    && latest.GetProperty("SongStorageReady").GetBoolean())
                {
                    return;
                }

                await Task.Delay(1000);
            }

            throw new TimeoutException("Timed out waiting for persistent side-effect probe state. Last state: " + latest);
        }

        private static IReadOnlyDictionary<string, FileFingerprint> Snapshot(string gameRoot)
        {
            Dictionary<string, FileFingerprint> snapshot = new Dictionary<string, FileFingerprint>(StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in MonitoredRelativePaths)
            {
                string fullPath = Path.Combine(gameRoot, relativePath);
                snapshot[Normalize(relativePath)] = FileFingerprint.Create(fullPath);
            }

            return snapshot;
        }

        private static void AssertNoPersistentDiffs(IReadOnlyDictionary<string, FileFingerprint> before, IReadOnlyDictionary<string, FileFingerprint> after)
        {
            List<string> diffs = new List<string>();
            foreach (string path in before.Keys.Union(after.Keys).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                FileFingerprint first = before.TryGetValue(path, out FileFingerprint beforeFingerprint)
                    ? beforeFingerprint
                    : FileFingerprint.Missing;
                FileFingerprint second = after.TryGetValue(path, out FileFingerprint afterFingerprint)
                    ? afterFingerprint
                    : FileFingerprint.Missing;

                if (!first.Equals(second))
                {
                    diffs.Add(path + ": " + first.Describe() + " -> " + second.Describe());
                }
            }

            Assert.True(
                diffs.Count == 0,
                "Persistent user/config/profile files changed during the test run:\n" + string.Join("\n", diffs));
        }

        private static Exception Combine(Exception first, Exception second)
        {
            if (first != null && second != null)
            {
                return new AggregateException(first, second);
            }

            return first ?? second;
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }

        private sealed class FileFingerprint : IEquatable<FileFingerprint>
        {
            public static readonly FileFingerprint Missing = new FileFingerprint(false, 0, string.Empty);

            private FileFingerprint(bool exists, long length, string sha256)
            {
                Exists = exists;
                Length = length;
                Sha256 = sha256;
            }

            public bool Exists { get; }
            public long Length { get; }
            public string Sha256 { get; }

            public static FileFingerprint Create(string path)
            {
                if (!File.Exists(path))
                {
                    return Missing;
                }

                using FileStream stream = File.OpenRead(path);
                using SHA256 sha256 = SHA256.Create();
                byte[] hash = sha256.ComputeHash(stream);
                return new FileFingerprint(true, stream.Length, Convert.ToHexString(hash));
            }

            public string Describe()
            {
                return Exists
                    ? "exists length=" + Length + " sha256=" + Sha256
                    : "missing";
            }

            public bool Equals(FileFingerprint other)
            {
                return other != null
                    && Exists == other.Exists
                    && Length == other.Length
                    && string.Equals(Sha256, other.Sha256, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as FileFingerprint);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Exists, Length, StringComparer.OrdinalIgnoreCase.GetHashCode(Sha256));
            }
        }
    }
}
