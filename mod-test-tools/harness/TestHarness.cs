using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ModTestHarness
{
    public sealed class TestHarness
    {
        public TestHarness(string projectRoot)
        {
            ProjectRoot = Path.GetFullPath(projectRoot);
            GameRoot = Path.Combine(ProjectRoot, "Majdata Hub", "game");
            ReadyPath = Path.Combine(GameRoot, "UserData", "TestHookMod", "ready.json");
            ArtifactsRoot = Path.Combine(ProjectRoot, ".scratch", "mod-test-tools-artifacts");
        }

        public string ProjectRoot { get; }
        public string GameRoot { get; }
        public string ReadyPath { get; }
        public string ArtifactsRoot { get; }

        public async Task<HarnessRun> LaunchAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            DateTimeOffset launchedAt = DateTimeOffset.UtcNow;
            DeleteStaleArtifacts();

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "powershell.exe";
            start.Arguments = "-NoProfile -Command \"Start-Process '.\\start-controller.bat'\"";
            start.WorkingDirectory = GameRoot;
            start.UseShellExecute = false;
            using (Process launcher = Process.Start(start))
            {
                launcher.WaitForExit(10000);
            }

            BridgeReadyFile ready = await WaitForReadyFileAsync(launchedAt, timeout, cancellationToken).ConfigureAwait(false);
            TestClient client = new TestClient(ready.Host, ready.Port);
            await WaitForHealthAsync(client, timeout, cancellationToken).ConfigureAwait(false);
            return new HarnessRun(this, ready, client);
        }

        public void CollectLogs(string artifactName)
        {
            string destination = Path.Combine(ArtifactsRoot, artifactName);
            Directory.CreateDirectory(destination);
            CopyIfExists(Path.Combine(GameRoot, "MelonLoader", "Latest.log"), Path.Combine(destination, "MelonLoader-Latest.log"));
            string logsDir = Path.Combine(GameRoot, "Logs");
            if (Directory.Exists(logsDir))
            {
                foreach (string file in Directory.GetFiles(logsDir))
                {
                    CopyIfExists(file, Path.Combine(destination, Path.GetFileName(file)));
                }
            }
        }

        public void KillReadinessPid(BridgeReadyFile ready)
        {
            if (ready == null || ready.Pid <= 0)
            {
                return;
            }

            try
            {
                Process process = Process.GetProcessById(ready.Pid);
                process.Kill(true);
            }
            catch
            {
            }
        }

        private void DeleteStaleArtifacts()
        {
            if (File.Exists(ReadyPath))
            {
                File.Delete(ReadyPath);
            }

            string tempReady = ReadyPath + ".tmp";
            if (File.Exists(tempReady))
            {
                File.Delete(tempReady);
            }

            Directory.CreateDirectory(ArtifactsRoot);
        }

        private async Task<BridgeReadyFile> WaitForReadyFileAsync(DateTimeOffset launchedAt, TimeSpan timeout, CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(ReadyPath))
                {
                    string json = File.ReadAllText(ReadyPath);
                    BridgeReadyFile ready = JsonSerializer.Deserialize<BridgeReadyFile>(json);
                    if (ready != null && IsFresh(ready, launchedAt))
                    {
                        return ready;
                    }
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out waiting for fresh TestHookMod readiness file.");
        }

        private static bool IsFresh(BridgeReadyFile ready, DateTimeOffset launchedAt)
        {
            DateTimeOffset startedAt;
            if (!DateTimeOffset.TryParse(ready.StartedAt, out startedAt))
            {
                return false;
            }

            return startedAt >= launchedAt.AddSeconds(-2);
        }

        private static async Task WaitForHealthAsync(TestClient client, TimeSpan timeout, CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    BridgeResponse health = await client.HealthAsync(cancellationToken).ConfigureAwait(false);
                    if (health.StatusCode == 200 && health.Content.Contains("\"ok\":true"))
                    {
                        return;
                    }
                }
                catch
                {
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out polling TestHookMod /health.");
        }

        private static void CopyIfExists(string source, string destination)
        {
            if (File.Exists(source))
            {
                File.Copy(source, destination, true);
            }
        }
    }

    public sealed class HarnessRun : IDisposable
    {
        private readonly TestHarness _harness;

        public HarnessRun(TestHarness harness, BridgeReadyFile ready, TestClient client)
        {
            _harness = harness;
            Ready = ready;
            Client = client;
        }

        public BridgeReadyFile Ready { get; }
        public TestClient Client { get; }

        public async Task ShutdownOrKillAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                BridgeResponse response = await Client.ShutdownAsync(cancellationToken).ConfigureAwait(false);
                if (response.StatusCode >= 200 && response.StatusCode < 300)
                {
                    return;
                }
            }
            catch
            {
            }

            _harness.KillReadinessPid(Ready);
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
