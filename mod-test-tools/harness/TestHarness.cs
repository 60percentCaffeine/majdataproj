using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ModTestHarness
{
    public sealed class TestHarness
    {
        private static readonly string[] RuntimeProcessNames = { "MajdataPlay", "ModTestReplClient" };

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
            return await LaunchAsync(timeout, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task<HarnessRun> LaunchAsync(TimeSpan timeout, TestHookLaunchOptions options, CancellationToken cancellationToken = default)
        {
            DateTimeOffset launchedAt = DateTimeOffset.UtcNow;
            await StopProcessesByNameAsync(RuntimeProcessNames, TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
            DeleteStaleArtifacts();
            using (ConfigFileScope configScope = ApplyLaunchOptions(options))
            {
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

        public async Task WaitForReadinessPidExitAsync(BridgeReadyFile ready, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (ready == null || ready.Pid <= 0)
            {
                return;
            }

            Process process;
            try
            {
                process = Process.GetProcessById(ready.Pid);
            }
            catch
            {
                return;
            }

            using (process)
            {
                DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (process.HasExited)
                    {
                        return;
                    }

                    await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                }
            }

            KillReadinessPid(ready);
        }

        public Task StopRuntimeProcessesAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return StopProcessesByNameAsync(RuntimeProcessNames, timeout, cancellationToken);
        }

        private void DeleteStaleArtifacts()
        {
            DeleteFileWithRetry(ReadyPath, TimeSpan.FromSeconds(10));
            DeleteFileWithRetry(ReadyPath + ".tmp", TimeSpan.FromSeconds(10));
            Directory.CreateDirectory(ArtifactsRoot);
        }

        private ConfigFileScope ApplyLaunchOptions(TestHookLaunchOptions options)
        {
            if (options == null || !options.HasAnyBridgeOption)
            {
                return ConfigFileScope.Empty;
            }

            string configPath = Path.Combine(GameRoot, "UserData", "TestHookMod", "config.json");
            ConfigFileScope scope = ConfigFileScope.Capture(configPath);
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            File.WriteAllText(configPath, options.ToConfigJson());
            return scope;
        }

        private static void DeleteFileWithRetry(string path, TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            while (true)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    return;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    File.Delete(path);
                    return;
                }

                Thread.Sleep(250);
            }
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

        private static async Task StopProcessesByNameAsync(string[] processNames, TimeSpan timeout, CancellationToken cancellationToken)
        {
            KillProcessesByName(processNames);

            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!AnyProcessesByName(processNames))
                {
                    return;
                }

                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }

            KillProcessesByName(processNames);
        }

        private static bool AnyProcessesByName(string[] processNames)
        {
            for (int i = 0; i < processNames.Length; i++)
            {
                if (Process.GetProcessesByName(processNames[i]).Length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void KillProcessesByName(string[] processNames)
        {
            for (int i = 0; i < processNames.Length; i++)
            {
                Process[] processes = Process.GetProcessesByName(processNames[i]);
                for (int j = 0; j < processes.Length; j++)
                {
                    using (Process process = processes[j])
                    {
                        try
                        {
                            process.Kill(true);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }
    }

    public sealed class TestHookLaunchOptions
    {
        public string BridgeHost { get; set; }
        public int? BridgePort { get; set; }
        public bool? ReplEnabled { get; set; }

        internal bool HasAnyBridgeOption
        {
            get { return BridgeHost != null || BridgePort.HasValue || ReplEnabled.HasValue; }
        }

        internal string ToConfigJson()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            bool wrote = false;
            if (BridgeHost != null)
            {
                AppendCommaIfNeeded(builder, ref wrote);
                builder.Append("\"host\":\"");
                builder.Append(Escape(BridgeHost));
                builder.Append("\"");
            }

            if (BridgePort.HasValue)
            {
                AppendCommaIfNeeded(builder, ref wrote);
                builder.Append("\"port\":");
                builder.Append(BridgePort.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (ReplEnabled.HasValue)
            {
                AppendCommaIfNeeded(builder, ref wrote);
                builder.Append("\"replEnabled\":");
                builder.Append(ReplEnabled.Value ? "true" : "false");
            }

            builder.Append("}");
            return builder.ToString();
        }

        private static void AppendCommaIfNeeded(StringBuilder builder, ref bool wrote)
        {
            if (wrote)
            {
                builder.Append(",");
            }

            wrote = true;
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal sealed class ConfigFileScope : IDisposable
    {
        public static readonly ConfigFileScope Empty = new ConfigFileScope(null, false, null);

        private readonly string _path;
        private readonly bool _existed;
        private readonly string _content;

        private ConfigFileScope(string path, bool existed, string content)
        {
            _path = path;
            _existed = existed;
            _content = content;
        }

        public static ConfigFileScope Capture(string path)
        {
            return new ConfigFileScope(path, File.Exists(path), File.Exists(path) ? File.ReadAllText(path) : null);
        }

        public void Dispose()
        {
            if (_path == null)
            {
                return;
            }

            if (_existed)
            {
                File.WriteAllText(_path, _content);
            }
            else if (File.Exists(_path))
            {
                File.Delete(_path);
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
                    await _harness.WaitForReadinessPidExitAsync(Ready, TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
                    await _harness.StopRuntimeProcessesAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
                    return;
                }
            }
            catch
            {
            }

            _harness.KillReadinessPid(Ready);
            await _harness.StopRuntimeProcessesAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
