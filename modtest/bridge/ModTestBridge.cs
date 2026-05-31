using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using MelonLoader;

[assembly: MelonInfo(typeof(ModTestBridge.ModTestBridgeMod), "ModTestBridge", ModTestBridge.ModTestBridgeMod.BridgeVersion, "user0")]
[assembly: MelonGame]
[assembly: HarmonyDontPatchAll]

namespace ModTestBridge
{
    public sealed class ModTestBridgeMod : MelonMod
    {
        public const string BridgeVersion = "0.1.0";

        private BridgeServer _server;
        private volatile bool _mainThreadDispatcherReady;

        public override void OnApplicationStart()
        {
            try
            {
                BridgeConfig.DeleteReadinessFile();
                BridgeConfig config = BridgeConfig.Load();
                _mainThreadDispatcherReady = true;
                _server = new BridgeServer(config, BridgeVersion, () => _mainThreadDispatcherReady);
                _server.Start();
                MelonLogger.Msg("ModTestBridge listening on http://" + config.Host + ":" + config.Port + " replEnabled=" + config.ReplEnabled);
            }
            catch (Exception ex)
            {
                MelonLogger.Error("ModTestBridge failed to start: " + ex);
                throw;
            }
        }

        public override void OnUpdate()
        {
            _mainThreadDispatcherReady = true;
        }

        public override void OnApplicationQuit()
        {
            BridgeServer server = _server;
            if (server != null)
            {
                server.Dispose();
                _server = null;
            }
        }
    }

    internal sealed class BridgeServer : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly bool _replEnabled;
        private readonly string _bridgeVersion;
        private readonly Func<bool> _mainThreadDispatcherReady;
        private readonly object _stopLock = new object();

        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _stopping;
        private string _startedAt;

        public BridgeServer(BridgeConfig config, string bridgeVersion, Func<bool> mainThreadDispatcherReady)
        {
            _host = config.Host;
            _port = config.Port;
            _replEnabled = config.ReplEnabled;
            _bridgeVersion = bridgeVersion;
            _mainThreadDispatcherReady = mainThreadDispatcherReady;
        }

        public void Start()
        {
            _startedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            _listener = new TcpListener(IPAddress.Parse(_host), _port);
            _listener.Start();

            WriteReadinessFile();

            _acceptThread = new Thread(AcceptLoop);
            _acceptThread.IsBackground = true;
            _acceptThread.Name = "ModTestBridge HTTP";
            _acceptThread.Start();
        }

        public void Dispose()
        {
            lock (_stopLock)
            {
                if (_stopping)
                {
                    return;
                }

                _stopping = true;
                if (_listener != null)
                {
                    _listener.Stop();
                }
            }
        }

        private void AcceptLoop()
        {
            while (!_stopping)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (SocketException)
                {
                    if (!_stopping)
                    {
                        MelonLogger.Warning("ModTestBridge accept loop socket error");
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (!_stopping)
                    {
                        MelonLogger.Error("ModTestBridge accept loop failed: " + ex);
                    }
                }
            }
        }

        private void HandleClient(object state)
        {
            TcpClient client = (TcpClient)state;
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    string requestLine = ReadRequestLine(stream);
                    DrainHeaders(stream);

                    if (requestLine == null)
                    {
                        WriteJson(stream, 400, "{\"ok\":false,\"error\":\"empty request\"}");
                        return;
                    }

                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2)
                    {
                        WriteJson(stream, 400, "{\"ok\":false,\"error\":\"malformed request\"}");
                        return;
                    }

                    if (parts[0] == "GET" && parts[1] == "/health")
                    {
                        WriteJson(stream, 200, HealthJson());
                        return;
                    }

                    WriteJson(stream, 404, "{\"ok\":false,\"error\":\"not found\"}");
                }
            }
            catch (Exception ex)
            {
                if (!_stopping)
                {
                    MelonLogger.Warning("ModTestBridge request failed: " + ex.Message);
                }
            }
            finally
            {
                client.Close();
            }
        }

        private string HealthJson()
        {
            return "{"
                + "\"ok\":true,"
                + "\"bridgeVersion\":\"" + JsonEscape(_bridgeVersion) + "\","
                + "\"pid\":" + CurrentPid().ToString(CultureInfo.InvariantCulture) + ","
                + "\"host\":\"" + JsonEscape(_host) + "\","
                + "\"port\":" + _port.ToString(CultureInfo.InvariantCulture) + ","
                + "\"replEnabled\":" + (_replEnabled ? "true" : "false") + ","
                + "\"mainThreadDispatcherReady\":" + (_mainThreadDispatcherReady() ? "true" : "false")
                + "}";
        }

        private void WriteReadinessFile()
        {
            string dir = BridgeConfig.ConfigDirectory;
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "ready.json");
            string tempPath = path + ".tmp";
            string json = "{"
                + "\"pid\":" + CurrentPid().ToString(CultureInfo.InvariantCulture) + ","
                + "\"host\":\"" + JsonEscape(_host) + "\","
                + "\"port\":" + _port.ToString(CultureInfo.InvariantCulture) + ","
                + "\"replEnabled\":" + (_replEnabled ? "true" : "false") + ","
                + "\"startedAt\":\"" + JsonEscape(_startedAt) + "\","
                + "\"bridgeVersion\":\"" + JsonEscape(_bridgeVersion) + "\""
                + "}";

            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
        }

        private static int CurrentPid()
        {
            return Process.GetCurrentProcess().Id;
        }

        private static string ReadRequestLine(Stream stream)
        {
            StringBuilder builder = new StringBuilder();
            int previous = -1;

            while (builder.Length < 8192)
            {
                int current = stream.ReadByte();
                if (current < 0)
                {
                    break;
                }

                if (previous == '\r' && current == '\n')
                {
                    builder.Length = builder.Length - 1;
                    break;
                }

                builder.Append((char)current);
                previous = current;
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        private static void DrainHeaders(Stream stream)
        {
            int matched = 0;
            while (matched < 4)
            {
                int current = stream.ReadByte();
                if (current < 0)
                {
                    return;
                }

                char expected = "\r\n\r\n"[matched];
                matched = current == expected ? matched + 1 : 0;
            }
        }

        private static void WriteJson(Stream stream, int statusCode, string body)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string reason = statusCode == 200 ? "OK" : statusCode == 400 ? "Bad Request" : "Not Found";
            string headers = "HTTP/1.1 " + statusCode.ToString(CultureInfo.InvariantCulture) + " " + reason + "\r\n"
                + "Content-Type: application/json; charset=utf-8\r\n"
                + "Content-Length: " + bodyBytes.Length.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + "Connection: close\r\n"
                + "\r\n";

            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
        }

        private static string JsonEscape(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal sealed class BridgeConfig
    {
        public const string DefaultHost = "127.0.0.1";
        public const int DefaultPort = 17443;
        public const bool DefaultReplEnabled = true;

        public string Host;
        public int Port;
        public bool ReplEnabled;

        public static string ConfigDirectory
        {
            get { return Path.Combine(Environment.CurrentDirectory, "UserData", "ModTestBridge"); }
        }

        public static BridgeConfig Load()
        {
            BridgeConfig config = new BridgeConfig();
            config.Host = DefaultHost;
            config.Port = DefaultPort;
            config.ReplEnabled = DefaultReplEnabled;

            config.ApplyConfigFile();
            config.ApplyEnvironment();
            config.ApplyCommandLine(Environment.GetCommandLineArgs());
            config.Validate("effective configuration");
            return config;
        }

        public static void DeleteReadinessFile()
        {
            string path = Path.Combine(ConfigDirectory, "ready.json");
            string tempPath = path + ".tmp";
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        private void ApplyConfigFile()
        {
            string path = Path.Combine(ConfigDirectory, "config.json");
            if (!File.Exists(path))
            {
                return;
            }

            string json = File.ReadAllText(path);
            string host = FindJsonString(json, "host");
            string port = FindJsonScalar(json, "port");
            string repl = FindFirstJsonScalar(json, new string[] { "replEnabled", "launchRepl", "repl" });

            if (host != null)
            {
                Host = host;
            }

            if (port != null)
            {
                Port = ParsePort(port, "UserData/ModTestBridge/config.json port");
            }

            if (repl != null)
            {
                ReplEnabled = ParseBool(repl, "UserData/ModTestBridge/config.json REPL setting");
            }
        }

        private void ApplyEnvironment()
        {
            string host = Environment.GetEnvironmentVariable("MODTEST_BRIDGE_HOST");
            string port = Environment.GetEnvironmentVariable("MODTEST_BRIDGE_PORT");
            string repl = Environment.GetEnvironmentVariable("MODTEST_BRIDGE_REPL");

            if (!IsNullOrEmpty(host))
            {
                Host = host;
            }

            if (!IsNullOrEmpty(port))
            {
                Port = ParsePort(port, "MODTEST_BRIDGE_PORT");
            }

            if (!IsNullOrEmpty(repl))
            {
                ReplEnabled = ParseBool(repl, "MODTEST_BRIDGE_REPL");
            }
        }

        private void ApplyCommandLine(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == null)
                {
                    continue;
                }

                if (arg == "--modtest-no-repl")
                {
                    ReplEnabled = false;
                    continue;
                }

                if (arg == "--modtest-repl")
                {
                    ReplEnabled = true;
                    continue;
                }

                string host = ReadOptionValue(args, ref i, "--modtest-host", arg);
                if (host != null)
                {
                    Host = host;
                    continue;
                }

                string port = ReadOptionValue(args, ref i, "--modtest-port", arg);
                if (port != null)
                {
                    Port = ParsePort(port, "--modtest-port");
                    continue;
                }
            }
        }

        private void Validate(string source)
        {
            IPAddress ignored;
            if (!IPAddress.TryParse(Host, out ignored))
            {
                throw new InvalidOperationException(source + " has invalid host '" + Host + "'. Use an IP address such as 127.0.0.1.");
            }

            if (Port < 1 || Port > 65535)
            {
                throw new InvalidOperationException(source + " has invalid port '" + Port.ToString(CultureInfo.InvariantCulture) + "'.");
            }
        }

        private static string ReadOptionValue(string[] args, ref int index, string optionName, string currentArg)
        {
            string prefix = optionName + "=";
            if (currentArg.StartsWith(prefix))
            {
                return currentArg.Substring(prefix.Length);
            }

            if (currentArg != optionName)
            {
                return null;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--"))
            {
                throw new InvalidOperationException(optionName + " requires a value.");
            }

            index++;
            return args[index];
        }

        private static int ParsePort(string value, string source)
        {
            int port;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535)
            {
                throw new InvalidOperationException(source + " must be an integer between 1 and 65535.");
            }

            return port;
        }

        private static bool ParseBool(string value, string source)
        {
            string normalized = value.Trim().ToLower(CultureInfo.InvariantCulture);
            if (normalized == "true" || normalized == "1" || normalized == "yes" || normalized == "on" || normalized == "enabled")
            {
                return true;
            }

            if (normalized == "false" || normalized == "0" || normalized == "no" || normalized == "off" || normalized == "disabled")
            {
                return false;
            }

            throw new InvalidOperationException(source + " must be true or false.");
        }

        private static string FindFirstJsonScalar(string json, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string value = FindJsonScalar(json, names[i]);
                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        private static string FindJsonString(string json, string name)
        {
            Regex regex = new Regex("\"" + Regex.Escape(name) + "\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
            Match match = regex.Match(json);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string FindJsonScalar(string json, string name)
        {
            string stringValue = FindJsonString(json, name);
            if (stringValue != null)
            {
                return stringValue;
            }

            Regex regex = new Regex("\"" + Regex.Escape(name) + "\"\\s*:\\s*([^,}\\s]+)", RegexOptions.IgnoreCase);
            Match match = regex.Match(json);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static bool IsNullOrEmpty(string value)
        {
            return value == null || value.Length == 0;
        }
    }
}
