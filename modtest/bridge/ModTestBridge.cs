using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

        private const string Host = "127.0.0.1";
        private const int Port = 17443;

        private BridgeServer _server;
        private volatile bool _mainThreadDispatcherReady;

        public override void OnApplicationStart()
        {
            try
            {
                _mainThreadDispatcherReady = true;
                _server = new BridgeServer(Host, Port, BridgeVersion, () => _mainThreadDispatcherReady);
                _server.Start();
                MelonLogger.Msg("ModTestBridge listening on http://" + Host + ":" + Port);
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
        private readonly string _bridgeVersion;
        private readonly Func<bool> _mainThreadDispatcherReady;
        private readonly object _stopLock = new object();

        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _stopping;
        private string _startedAt;

        public BridgeServer(string host, int port, string bridgeVersion, Func<bool> mainThreadDispatcherReady)
        {
            _host = host;
            _port = port;
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
                + "\"mainThreadDispatcherReady\":" + (_mainThreadDispatcherReady() ? "true" : "false")
                + "}";
        }

        private void WriteReadinessFile()
        {
            string dir = Path.Combine(Environment.CurrentDirectory, "UserData", "ModTestBridge");
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "ready.json");
            string tempPath = path + ".tmp";
            string json = "{"
                + "\"pid\":" + CurrentPid().ToString(CultureInfo.InvariantCulture) + ","
                + "\"host\":\"" + JsonEscape(_host) + "\","
                + "\"port\":" + _port.ToString(CultureInfo.InvariantCulture) + ","
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
}
