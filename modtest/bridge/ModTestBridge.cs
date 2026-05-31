using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MelonLoader;

[assembly: MelonInfo(typeof(ModTestBridge.TestHookMod), "Test Hook Mod", ModTestBridge.TestHookMod.BridgeVersion, "user0")]
[assembly: MelonGame]
[assembly: HarmonyDontPatchAll]

namespace ModTestBridge
{
    public sealed class TestHookMod : MelonMod
    {
        public const string BridgeVersion = "0.1.0";

        private BridgeServer _server;
        private EvalDispatcher _evalDispatcher;
        private volatile bool _mainThreadDispatcherReady;

        public override void OnApplicationStart()
        {
            try
            {
                BridgeConfig.DeleteReadinessFile();
                RoslynDependencyLoader.Install();
                BridgeConfig config = BridgeConfig.Load();
                _evalDispatcher = new EvalDispatcher(SynchronizationContext.Current);
                _mainThreadDispatcherReady = true;
                _server = new BridgeServer(config, BridgeVersion, () => _mainThreadDispatcherReady, _evalDispatcher);
                _server.Start();
                MelonLogger.Msg("Test Hook Mod listening on http://" + config.Host + ":" + config.Port + " replEnabled=" + config.ReplEnabled);
                if (config.ReplEnabled)
                {
                    ReplLauncher.LaunchOrReveal(config);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Test Hook Mod failed to start: " + ex);
                throw;
            }
        }

        public override void OnUpdate()
        {
            _mainThreadDispatcherReady = true;
            EvalDispatcher dispatcher = _evalDispatcher;
            if (dispatcher != null)
            {
                dispatcher.Update();
            }
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
        private readonly EvalDispatcher _evalDispatcher;
        private readonly object _stopLock = new object();

        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _stopping;
        private string _startedAt;

        public BridgeServer(BridgeConfig config, string bridgeVersion, Func<bool> mainThreadDispatcherReady, EvalDispatcher evalDispatcher)
        {
            _host = config.Host;
            _port = config.Port;
            _replEnabled = config.ReplEnabled;
            _bridgeVersion = bridgeVersion;
            _mainThreadDispatcherReady = mainThreadDispatcherReady;
            _evalDispatcher = evalDispatcher;
        }

        public void Start()
        {
            _startedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            _listener = new TcpListener(IPAddress.Parse(_host), _port);
            _listener.Start();

            WriteReadinessFile();

            _acceptThread = new Thread(AcceptLoop);
            _acceptThread.IsBackground = true;
            _acceptThread.Name = "Test Hook Mod HTTP";
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
                        MelonLogger.Warning("Test Hook Mod accept loop socket error");
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
                        MelonLogger.Error("Test Hook Mod accept loop failed: " + ex);
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
                    Dictionary<string, string> headers = ReadHeaders(stream);

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

                    if (parts[0] == "POST" && parts[1] == "/eval-isolated")
                    {
                        int contentLength = ContentLength(headers);
                        string body = ReadBody(stream, contentLength);
                        EvalRequest request = EvalRequest.FromJson(body);
                        EvalResponse response = _evalDispatcher.EvaluateIsolated(request);
                        WriteJson(stream, response.StatusCode, response.Json);
                        return;
                    }

                    if (parts[0] == "POST" && parts[1] == "/eval")
                    {
                        int contentLength = ContentLength(headers);
                        string body = ReadBody(stream, contentLength);
                        EvalRequest request = EvalRequest.FromJson(body);
                        EvalResponse response = _evalDispatcher.EvaluatePersistent(request);
                        WriteJson(stream, response.StatusCode, response.Json);
                        return;
                    }

                    if (parts[0] == "POST" && parts[1] == "/reset-session")
                    {
                        int contentLength = ContentLength(headers);
                        ReadBody(stream, contentLength);
                        _evalDispatcher.ResetSession();
                        WriteJson(stream, 200, "{\"ok\":true,\"phase\":\"reset\",\"sessionVersion\":" + _evalDispatcher.SessionVersion.ToString(CultureInfo.InvariantCulture) + "}");
                        return;
                    }

                    if (parts[0] == "POST" && parts[1] == "/shutdown")
                    {
                        int contentLength = ContentLength(headers);
                        ReadBody(stream, contentLength);
                        bool accepted = _evalDispatcher.RequestShutdown();
                        WriteJson(stream, accepted ? 200 : 500, "{\"ok\":" + (accepted ? "true" : "false") + ",\"phase\":\"shutdown\",\"accepted\":" + (accepted ? "true" : "false") + ",\"fallbackPid\":" + CurrentPid().ToString(CultureInfo.InvariantCulture) + "}");
                        return;
                    }

                    WriteJson(stream, 404, "{\"ok\":false,\"error\":\"not found\"}");
                }
            }
            catch (Exception ex)
            {
                if (!_stopping)
                {
                    MelonLogger.Warning("Test Hook Mod request failed: " + ex.Message);
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

        private static Dictionary<string, string> ReadHeaders(Stream stream)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            StringBuilder line = new StringBuilder();
            int matched = 0;
            while (matched < 4)
            {
                int current = stream.ReadByte();
                if (current < 0)
                {
                    return headers;
                }

                char expected = "\r\n\r\n"[matched];
                matched = current == expected ? matched + 1 : 0;
                line.Append((char)current);
            }

            string[] headerLines = line.ToString().Split(new string[] { "\r\n" }, StringSplitOptions.None);
            for (int i = 0; i < headerLines.Length; i++)
            {
                string headerLine = headerLines[i];
                int colon = headerLine.IndexOf(':');
                if (colon > 0)
                {
                    headers[headerLine.Substring(0, colon).Trim()] = headerLine.Substring(colon + 1).Trim();
                }
            }

            return headers;
        }

        private static int ContentLength(Dictionary<string, string> headers)
        {
            string value;
            if (!headers.TryGetValue("Content-Length", out value))
            {
                return 0;
            }

            int length;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out length) || length < 0)
            {
                throw new InvalidOperationException("Invalid Content-Length header.");
            }

            return length;
        }

        private static string ReadBody(Stream stream, int contentLength)
        {
            if (contentLength <= 0)
            {
                return string.Empty;
            }

            byte[] bytes = new byte[contentLength];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read <= 0)
                {
                    break;
                }

                offset += read;
            }

            return Encoding.UTF8.GetString(bytes, 0, offset);
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

    internal static class ReplLauncher
    {
        public static void LaunchOrReveal(BridgeConfig config)
        {
            try
            {
                string exePath = Path.Combine(Environment.CurrentDirectory, "Mods", "ModTestReplClient", "ModTestReplClient.exe");
                if (!File.Exists(exePath))
                {
                    MelonLogger.Warning("Test Hook Mod REPL client is enabled but not installed at " + exePath);
                    return;
                }

                string command = "start \"Test Hook Mod REPL\" \"" + exePath + "\" --host " + config.Host + " --port " + config.Port.ToString(CultureInfo.InvariantCulture);
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = "cmd.exe";
                start.Arguments = "/c " + command;
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                Process.Start(start);
                MelonLogger.Msg("Launched Test Hook Mod REPL client.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to launch Test Hook Mod REPL client: " + ex.Message);
            }
        }
    }

    public sealed class EvalGlobals
    {
        public string BridgeVersion
        {
            get { return TestHookMod.BridgeVersion; }
        }
    }

    public static class BridgeUtilities
    {
        public static void Log(string message)
        {
            MelonLogger.Msg(message);
        }
    }

    internal sealed class EvalDispatcher
    {
        private readonly SynchronizationContext _mainThreadContext;
        private readonly RoslynEvalService _evalService = new RoslynEvalService();
        private readonly object _sessionLock = new object();
        private readonly List<string> _sessionUsings = new List<string>();
        private readonly List<string> _sessionStatements = new List<string>();
        private int _sessionVersion;

        public EvalDispatcher(SynchronizationContext mainThreadContext)
        {
            _mainThreadContext = mainThreadContext;
        }

        public EvalResponse EvaluateIsolated(EvalRequest request)
        {
            EvalWorkItem item = new EvalWorkItem(request, _evalService, EvalMode.Isolated, null);
            return Evaluate(item, request);
        }

        public EvalResponse EvaluatePersistent(EvalRequest request)
        {
            SessionSnapshot snapshot;
            lock (_sessionLock)
            {
                snapshot = new SessionSnapshot(_sessionUsings.ToArray(), _sessionStatements.ToArray(), _sessionVersion);
            }

            EvalWorkItem item = new EvalWorkItem(request, _evalService, EvalMode.Persistent, snapshot);
            EvalResponse response = Evaluate(item, request);
            if (response.StatusCode == 200 && item.SessionUpdate != null)
            {
                lock (_sessionLock)
                {
                    if (item.SessionUpdate.UsingDirective != null && !_sessionUsings.Contains(item.SessionUpdate.UsingDirective))
                    {
                        _sessionUsings.Add(item.SessionUpdate.UsingDirective);
                    }

                    if (item.SessionUpdate.Statement != null)
                    {
                        _sessionStatements.Add(item.SessionUpdate.Statement);
                    }
                }
            }

            return response;
        }

        public void ResetSession()
        {
            lock (_sessionLock)
            {
                _sessionUsings.Clear();
                _sessionStatements.Clear();
                _sessionVersion++;
            }
        }

        public int SessionVersion
        {
            get
            {
                lock (_sessionLock)
                {
                    return _sessionVersion;
                }
            }
        }

        public bool RequestShutdown()
        {
            try
            {
                if (_mainThreadContext != null)
                {
                    _mainThreadContext.Post(ShutdownOnMainThread, null);
                }
                else
                {
                    ShutdownOnMainThread(null);
                }

                Thread exitThread = new Thread(ExitProcessAfterGracePeriod);
                exitThread.IsBackground = true;
                exitThread.Name = "Test Hook Mod shutdown fallback";
                exitThread.Start();
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Shutdown request failed: " + ex.Message);
                return false;
            }
        }

        private EvalResponse Evaluate(EvalWorkItem item, EvalRequest request)
        {
            if (_mainThreadContext != null)
            {
                _mainThreadContext.Post(ExecuteOnMainThread, item);
            }
            else
            {
                item.StartOnCurrentThread();
            }

            int timeoutMs = request.TimeoutMs <= 0 ? 5000 : request.TimeoutMs;
            if (!item.Wait(timeoutMs + 250))
            {
                return EvalResponse.Timeout();
            }

            return item.Response;
        }

        public void Update()
        {
        }

        private static void ExecuteOnMainThread(object state)
        {
            ((EvalWorkItem)state).StartOnCurrentThread();
        }

        private static void ShutdownOnMainThread(object state)
        {
            MelonLogger.Msg("Test Hook Mod shutdown requested.");
            Type applicationType = typeof(UnityEngine.Application);
            MethodInfo quit = applicationType.GetMethod("Quit", new Type[0]);
            if (quit == null)
            {
                quit = applicationType.GetMethod("Quit", new Type[] { typeof(int) });
            }

            if (quit == null)
            {
                throw new MissingMethodException("UnityEngine.Application.Quit");
            }

            if (quit.GetParameters().Length == 0)
            {
                quit.Invoke(null, null);
            }
            else
            {
                quit.Invoke(null, new object[] { 0 });
            }
        }

        private static void ExitProcessAfterGracePeriod()
        {
            Thread.Sleep(2000);
            MelonLogger.Msg("Test Hook Mod forcing process exit after graceful shutdown request.");
            TerminateProcess(GetCurrentProcess(), 0);
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern bool TerminateProcess(IntPtr processHandle, uint exitCode);
    }

    internal sealed class EvalWorkItem
    {
        private readonly EvalRequest _request;
        private readonly RoslynEvalService _evalService;
        private readonly EvalMode _mode;
        private readonly SessionSnapshot _sessionSnapshot;
        private readonly ManualResetEvent _done = new ManualResetEvent(false);

        public EvalResponse Response;
        public SessionUpdate SessionUpdate;

        public EvalWorkItem(EvalRequest request, RoslynEvalService evalService, EvalMode mode, SessionSnapshot sessionSnapshot)
        {
            _request = request;
            _evalService = evalService;
            _mode = mode;
            _sessionSnapshot = sessionSnapshot;
        }

        public bool Wait(int timeoutMs)
        {
            return _done.WaitOne(timeoutMs);
        }

        public void StartOnCurrentThread()
        {
            if (Response != null)
            {
                return;
            }

            try
            {
                EvalStartResult startResult = _evalService.Start(_request, _mode, _sessionSnapshot);
                SessionUpdate = startResult.SessionUpdate;
                Task<object> task = startResult.Task;
                task.ContinueWith(delegate(Task<object> completed)
                {
                    if (completed.IsFaulted)
                    {
                        Response = EvalResponse.ExecutionError(completed.Exception == null ? null : completed.Exception.GetBaseException());
                    }
                    else if (completed.IsCanceled)
                    {
                        Response = EvalResponse.ExecutionError("Task was canceled.");
                    }
                    else
                    {
                        Response = EvalResponse.Success(completed.Result, _request);
                    }

                    _done.Set();
                });
            }
            catch (CompilationException ex)
            {
                Response = EvalResponse.CompilationError(ex.DiagnosticsJson);
                _done.Set();
            }
            catch (Exception ex)
            {
                Response = EvalResponse.ExecutionError(ex);
                _done.Set();
            }
        }
    }

    internal enum EvalMode
    {
        Isolated,
        Persistent
    }

    internal sealed class SessionSnapshot
    {
        public readonly string[] Usings;
        public readonly string[] Statements;
        public readonly int Version;

        public SessionSnapshot(string[] usings, string[] statements, int version)
        {
            Usings = usings;
            Statements = statements;
            Version = version;
        }
    }

    internal sealed class SessionUpdate
    {
        public string UsingDirective;
        public string Statement;
    }

    internal sealed class EvalStartResult
    {
        public Task<object> Task;
        public SessionUpdate SessionUpdate;
    }

    internal sealed class EvalRequest
    {
        public string Code;
        public int TimeoutMs;
        public int MaxDepth;
        public int MaxResponseBytes;

        public static EvalRequest FromJson(string json)
        {
            EvalRequest request = new EvalRequest();
            request.Code = JsonTools.FindJsonString(json, "code");
            request.TimeoutMs = JsonTools.FindJsonInt(json, "timeoutMs", 5000);
            request.MaxDepth = JsonTools.FindJsonInt(json, "maxDepth", 4);
            request.MaxResponseBytes = JsonTools.FindJsonInt(json, "maxResponseBytes", 65536);

            if (request.Code == null)
            {
                throw new InvalidOperationException("POST /eval-isolated requires a JSON string field named code.");
            }

            return request;
        }
    }

    internal sealed class EvalResponse
    {
        public int StatusCode;
        public string Json;

        public static EvalResponse Success(object value, EvalRequest request)
        {
            try
            {
                return new EvalResponse
                {
                    StatusCode = 200,
                    Json = "{\"ok\":true,\"phase\":\"execution\",\"result\":" + BoundedJsonSerializer.Serialize(value, request.MaxDepth, request.MaxResponseBytes) + "}"
                };
            }
            catch (SerializationLimitException ex)
            {
                return SerializationError(ex.Message);
            }
            catch (Exception ex)
            {
                return SerializationError(ex.Message);
            }
        }

        public static EvalResponse SerializationError(string message)
        {
            return new EvalResponse
            {
                StatusCode = 500,
                Json = "{\"ok\":false,\"phase\":\"serialization\",\"error\":{\"type\":\"SerializationException\",\"message\":\"" + JsonTools.Escape(message) + "\"}}"
            };
        }

        public static EvalResponse CompilationError(string diagnosticsJson)
        {
            return new EvalResponse
            {
                StatusCode = 400,
                Json = "{\"ok\":false,\"phase\":\"compilation\",\"errors\":" + diagnosticsJson + "}"
            };
        }

        public static EvalResponse ExecutionError(Exception ex)
        {
            if (ex == null)
            {
                return ExecutionError("Execution failed.");
            }

            return new EvalResponse
            {
                StatusCode = 500,
                Json = "{\"ok\":false,\"phase\":\"execution\",\"error\":{\"type\":\"" + JsonTools.Escape(ex.GetType().FullName) + "\",\"message\":\"" + JsonTools.Escape(ex.Message) + "\"}}"
            };
        }

        public static EvalResponse ExecutionError(string message)
        {
            return new EvalResponse
            {
                StatusCode = 500,
                Json = "{\"ok\":false,\"phase\":\"execution\",\"error\":{\"type\":\"System.Exception\",\"message\":\"" + JsonTools.Escape(message) + "\"}}"
            };
        }

        public static EvalResponse Timeout()
        {
            return new EvalResponse
            {
                StatusCode = 408,
                Json = "{\"ok\":false,\"phase\":\"execution\",\"error\":{\"type\":\"TimeoutException\",\"message\":\"Evaluation timed out.\"}}"
            };
        }
    }

    internal sealed class SerializationLimitException : Exception
    {
        public SerializationLimitException(string message)
            : base(message)
        {
        }
    }

    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y)
        {
            return object.ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }

    internal sealed class BoundedJsonSerializer
    {
        private readonly int _maxDepth;
        private readonly int _maxResponseBytes;
        private readonly StringBuilder _builder = new StringBuilder();
        private readonly Dictionary<object, int> _seen = new Dictionary<object, int>(new ReferenceEqualityComparer());
        private int _nextId = 1;

        private BoundedJsonSerializer(int maxDepth, int maxResponseBytes)
        {
            _maxDepth = maxDepth <= 0 ? 4 : maxDepth;
            _maxResponseBytes = maxResponseBytes <= 0 ? 65536 : maxResponseBytes;
        }

        public static string Serialize(object value, int maxDepth, int maxResponseBytes)
        {
            BoundedJsonSerializer serializer = new BoundedJsonSerializer(maxDepth, maxResponseBytes);
            serializer.WriteValue(value, 0);
            return serializer._builder.ToString();
        }

        private void WriteValue(object value, int depth)
        {
            CheckSize();
            if (value == null)
            {
                Append("null");
                return;
            }

            Type type = value.GetType();
            if (IsPrimitiveLike(type))
            {
                WritePrimitive(value);
                return;
            }

            if (IsUnsupported(type, value))
            {
                WriteUnsupported(value, type);
                return;
            }

            if (depth >= _maxDepth)
            {
                throw new SerializationLimitException("Maximum serialization depth exceeded.");
            }

            int existingId;
            if (_seen.TryGetValue(value, out existingId))
            {
                Append("{\"$ref\":");
                Append(existingId.ToString(CultureInfo.InvariantCulture));
                Append("}");
                return;
            }

            int id = _nextId++;
            _seen[value] = id;

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                WriteDictionary(dictionary, id, depth);
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                WriteEnumerable(enumerable, id, depth);
                return;
            }

            WriteObject(value, type, id, depth);
        }

        private void WriteDictionary(IDictionary dictionary, int id, int depth)
        {
            Append("{\"$id\":");
            Append(id.ToString(CultureInfo.InvariantCulture));
            Append(",\"type\":\"");
            Append(JsonTools.Escape(dictionary.GetType().FullName));
            Append("\",\"entries\":[");

            bool first = true;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (!first)
                {
                    Append(",");
                }

                first = false;
                Append("{\"key\":");
                WriteValue(entry.Key, depth + 1);
                Append(",\"value\":");
                WriteValue(entry.Value, depth + 1);
                Append("}");
            }

            Append("]}");
        }

        private void WriteEnumerable(IEnumerable enumerable, int id, int depth)
        {
            Append("{\"$id\":");
            Append(id.ToString(CultureInfo.InvariantCulture));
            Append(",\"type\":\"");
            Append(JsonTools.Escape(enumerable.GetType().FullName));
            Append("\",\"items\":[");

            bool first = true;
            foreach (object item in enumerable)
            {
                if (!first)
                {
                    Append(",");
                }

                first = false;
                WriteValue(item, depth + 1);
            }

            Append("]}");
        }

        private void WriteObject(object value, Type type, int id, int depth)
        {
            Append("{\"$id\":");
            Append(id.ToString(CultureInfo.InvariantCulture));
            Append(",\"type\":\"");
            Append(JsonTools.Escape(type.FullName));
            Append("\",\"properties\":{");

            bool first = true;
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                object propertyValue;
                try
                {
                    propertyValue = property.GetValue(value, null);
                }
                catch (Exception ex)
                {
                    propertyValue = "property read failed: " + ex.GetType().Name;
                }

                if (!first)
                {
                    Append(",");
                }

                first = false;
                Append("\"");
                Append(JsonTools.Escape(property.Name));
                Append("\":");
                WriteValue(propertyValue, depth + 1);
            }

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object fieldValue = field.GetValue(value);
                if (!first)
                {
                    Append(",");
                }

                first = false;
                Append("\"");
                Append(JsonTools.Escape(field.Name));
                Append("\":");
                WriteValue(fieldValue, depth + 1);
            }

            Append("}}");
        }

        private void WritePrimitive(object value)
        {
            if (value is string || value is char)
            {
                Append("\"");
                Append(JsonTools.Escape(Convert.ToString(value, CultureInfo.InvariantCulture)));
                Append("\"");
                return;
            }

            if (value is bool)
            {
                Append((bool)value ? "true" : "false");
                return;
            }

            if (value is DateTime)
            {
                Append("\"");
                Append(((DateTime)value).ToString("o", CultureInfo.InvariantCulture));
                Append("\"");
                return;
            }

            if (value is Enum)
            {
                Append("\"");
                Append(JsonTools.Escape(value.ToString()));
                Append("\"");
                return;
            }

            Append(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private void WriteUnsupported(object value, Type type)
        {
            Append("{\"unsupported\":true,\"type\":\"");
            Append(JsonTools.Escape(type.FullName));
            Append("\",\"id\":\"");
            Append(JsonTools.Escape(type.FullName + "@" + RuntimeHelpers.GetHashCode(value).ToString("x", CultureInfo.InvariantCulture)));
            Append("\",\"string\":\"");
            Append(JsonTools.Escape(SafeToString(value)));
            Append("\"}");
        }

        private static bool IsPrimitiveLike(Type type)
        {
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(char)
                || type == typeof(decimal)
                || type == typeof(DateTime);
        }

        private static bool IsUnsupported(Type type, object value)
        {
            return typeof(Delegate).IsAssignableFrom(type)
                || typeof(IntPtr) == type
                || typeof(UIntPtr) == type
                || typeof(Stream).IsAssignableFrom(type)
                || typeof(Task).IsAssignableFrom(type)
                || typeof(MemberInfo).IsAssignableFrom(type)
                || typeof(Type).IsAssignableFrom(type)
                || type.IsPointer
                || type.FullName != null && type.FullName.IndexOf("UnityEngine.Object") >= 0;
        }

        private static string SafeToString(object value)
        {
            try
            {
                return value == null ? string.Empty : value.ToString();
            }
            catch (Exception ex)
            {
                return "ToString failed: " + ex.GetType().Name;
            }
        }

        private void Append(string value)
        {
            _builder.Append(value);
            CheckSize();
        }

        private void CheckSize()
        {
            if (Encoding.UTF8.GetByteCount(_builder.ToString()) > _maxResponseBytes)
            {
                throw new SerializationLimitException("Maximum response size exceeded.");
            }
        }
    }

    internal sealed class RoslynEvalService
    {
        public EvalStartResult Start(EvalRequest request, EvalMode mode, SessionSnapshot sessionSnapshot)
        {
            CompileResult compileResult = mode == EvalMode.Persistent ? CompilePersistent(request.Code, sessionSnapshot) : CompileIsolated(request.Code);
            Task<object> task = StartCompiled(compileResult.Assembly);
            return new EvalStartResult { Task = task, SessionUpdate = compileResult.SessionUpdate };
        }

        private Task<object> StartCompiled(Assembly assembly)
        {
            Type type = assembly.GetType("__ModTestBridgeEval.Snippet");
            MethodInfo method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
            object result = method.Invoke(null, new object[] { new EvalGlobals() });

            Task<object> task = result as Task<object>;
            if (task != null)
            {
                return task;
            }

            TaskCompletionSource<object> source = new TaskCompletionSource<object>();
            source.SetResult(result);
            return source.Task;
        }

        private CompileResult CompileIsolated(string code)
        {
            CompilationException statementFailure;
            try
            {
                return new CompileResult { Assembly = CompileWrapped(null, null, ExpressionBody(code)), SessionUpdate = null };
            }
            catch (CompilationException ex)
            {
                statementFailure = ex;
            }

            try
            {
                return new CompileResult { Assembly = CompileWrapped(null, null, code + "\r\nreturn Task.FromResult<object>(null);"), SessionUpdate = null };
            }
            catch (CompilationException)
            {
                throw statementFailure;
            }
        }

        private CompileResult CompilePersistent(string code, SessionSnapshot sessionSnapshot)
        {
            string trimmed = code == null ? string.Empty : code.Trim();
            if (IsUsingDirective(trimmed))
            {
                string normalizedUsing = NormalizeUsing(trimmed);
                string[] usingsWithNew = AddUsing(sessionSnapshot.Usings, normalizedUsing);
                Assembly assembly = CompileWrapped(usingsWithNew, sessionSnapshot.Statements, "return Task.FromResult<object>(null);");
                return new CompileResult { Assembly = assembly, SessionUpdate = new SessionUpdate { UsingDirective = normalizedUsing } };
            }

            CompilationException expressionFailure;
            try
            {
                Assembly assembly = CompileWrapped(sessionSnapshot.Usings, sessionSnapshot.Statements, ExpressionBody(code));
                return new CompileResult { Assembly = assembly, SessionUpdate = null };
            }
            catch (CompilationException ex)
            {
                expressionFailure = ex;
            }

            try
            {
                string statementBody = code + "\r\nreturn Task.FromResult<object>(null);";
                Assembly assembly = CompileWrapped(sessionSnapshot.Usings, sessionSnapshot.Statements, statementBody);
                return new CompileResult { Assembly = assembly, SessionUpdate = new SessionUpdate { Statement = code } };
            }
            catch (CompilationException)
            {
                throw expressionFailure;
            }
        }

        private static string ExpressionBody(string code)
        {
            string trimmed = code == null ? string.Empty : code.Trim();
            if (trimmed.StartsWith("await "))
            {
                string awaited = trimmed.Substring("await ".Length);
                return "object __taskObject = (object)(" + awaited + ");\r\n"
                    + "Task __task = (Task)__taskObject;\r\n"
                    + "__task.Wait();\r\n"
                    + "Type __taskType = __taskObject.GetType();\r\n"
                    + "PropertyInfo __resultProperty = __taskType.GetProperty(\"Result\");\r\n"
                    + "return Task.FromResult<object>(__resultProperty == null ? null : __resultProperty.GetValue(__taskObject, null));";
            }

            return "return Task.FromResult<object>((object)(" + code + "));";
        }

        private Assembly CompileWrapped(string[] sessionUsings, string[] sessionStatements, string body)
        {
            StringBuilder sourceBuilder = new StringBuilder();
            AppendDefaultUsings(sourceBuilder);
            if (sessionUsings != null)
            {
                for (int i = 0; i < sessionUsings.Length; i++)
                {
                    sourceBuilder.Append(sessionUsings[i]);
                    sourceBuilder.Append("\r\n");
                }
            }

            sourceBuilder.Append("namespace __ModTestBridgeEval {\r\n");
            sourceBuilder.Append("  public static class Snippet {\r\n");
            sourceBuilder.Append("    public static Task<object> Run(EvalGlobals globals) {\r\n");
            if (sessionStatements != null)
            {
                for (int i = 0; i < sessionStatements.Length; i++)
                {
                    sourceBuilder.Append(sessionStatements[i]);
                    sourceBuilder.Append("\r\n");
                }
            }

            sourceBuilder.Append(body);
            sourceBuilder.Append("\r\n    }\r\n  }\r\n}\r\n");
            string source = sourceBuilder.ToString();

            string evalDir = Path.Combine(BridgeConfig.ConfigDirectory, "eval");
            Directory.CreateDirectory(evalDir);
            string id = Guid.NewGuid().ToString("N");
            string sourcePath = Path.Combine(evalDir, id + ".cs");
            string outputPath = Path.Combine(evalDir, id + ".dll");
            File.WriteAllText(sourcePath, source);

            string compilerOutput = RunRoslynCompiler(sourcePath, outputPath);
            if (!File.Exists(outputPath))
            {
                throw new CompilationException(DiagnosticsJson(compilerOutput));
            }

            byte[] bytes = File.ReadAllBytes(outputPath);
            return Assembly.Load(bytes);
        }

        private static void AppendDefaultUsings(StringBuilder sourceBuilder)
        {
            sourceBuilder.Append("using System;\r\n");
            sourceBuilder.Append("using System.Linq;\r\n");
            sourceBuilder.Append("using System.Collections.Generic;\r\n");
            sourceBuilder.Append("using System.Reflection;\r\n");
            sourceBuilder.Append("using System.Threading.Tasks;\r\n");
            sourceBuilder.Append("using MelonLoader;\r\n");
            sourceBuilder.Append("using UnityEngine;\r\n");
            sourceBuilder.Append("using ModTestBridge;\r\n");
        }

        private static bool IsUsingDirective(string code)
        {
            return code.StartsWith("using ") && code.EndsWith(";");
        }

        private static string NormalizeUsing(string code)
        {
            return code.Trim();
        }

        private static string[] AddUsing(string[] current, string value)
        {
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] == value)
                {
                    return current;
                }
            }

            string[] next = new string[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = value;
            return next;
        }

        private static string RunRoslynCompiler(string sourcePath, string outputPath)
        {
            string dotnetPath = @"C:\Program Files\dotnet\dotnet.exe";
            string cscPath = @"C:\Program Files\dotnet\sdk\9.0.200\Roslyn\bincore\csc.dll";

            StringBuilder arguments = new StringBuilder();
            arguments.Append("\"");
            arguments.Append(cscPath);
            arguments.Append("\" /nologo /target:library /optimize+ /nostdlib+ /out:\"");
            arguments.Append(outputPath);
            arguments.Append("\"");

            string[] references = ReferencePaths();
            for (int i = 0; i < references.Length; i++)
            {
                arguments.Append(" /reference:\"");
                arguments.Append(references[i]);
                arguments.Append("\"");
            }

            arguments.Append(" \"");
            arguments.Append(sourcePath);
            arguments.Append("\"");

            string logPath = outputPath + ".log";
            string commandPath = outputPath + ".cmd";
            string command = "@\"" + dotnetPath + "\" " + arguments + " > \"" + logPath + "\" 2>&1\r\nexit /b %ERRORLEVEL%\r\n";
            File.WriteAllText(commandPath, command);
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "cmd.exe";
            start.Arguments = "/c \"" + commandPath + "\"";
            start.UseShellExecute = false;
            start.CreateNoWindow = true;

            using (Process process = Process.Start(start))
            {
                process.WaitForExit();
                string output = File.Exists(logPath) ? File.ReadAllText(logPath) : string.Empty;
                if (process.ExitCode != 0)
                {
                    return output;
                }

                return output;
            }
        }

        private static string[] ReferencePaths()
        {
            List<string> references = new List<string>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null || assembly.IsDynamic)
                {
                    continue;
                }

                string location;
                try
                {
                    location = assembly.Location;
                }
                catch
                {
                    continue;
                }

                if (location == null || location.Length == 0 || !File.Exists(location))
                {
                    continue;
                }

                if (!references.Contains(location))
                {
                    references.Add(location);
                }
            }

            string bridgeAssemblyPath = Path.Combine(Environment.CurrentDirectory, "Mods", "TestHookMod.dll");
            if (File.Exists(bridgeAssemblyPath) && !references.Contains(bridgeAssemblyPath))
            {
                references.Add(bridgeAssemblyPath);
            }

            string legacyBridgeAssemblyPath = Path.Combine(Environment.CurrentDirectory, "Mods", "ModTestBridge.dll");
            if (File.Exists(legacyBridgeAssemblyPath) && !references.Contains(legacyBridgeAssemblyPath))
            {
                references.Add(legacyBridgeAssemblyPath);
            }

            string modsDir = Path.Combine(Environment.CurrentDirectory, "Mods");
            if (Directory.Exists(modsDir))
            {
                string[] modAssemblies = Directory.GetFiles(modsDir, "*.dll");
                for (int i = 0; i < modAssemblies.Length; i++)
                {
                    AddReferenceIfExists(references, modAssemblies[i]);
                }
            }

            string managedDir = Path.Combine(Environment.CurrentDirectory, "MajdataPlay_Data", "Managed");
            AddReferenceIfExists(references, Path.Combine(managedDir, "System.Threading.Tasks.dll"));
            AddReferenceIfExists(references, Path.Combine(managedDir, "System.Threading.Tasks.Extensions.dll"));

            return references.ToArray();
        }

        private static void AddReferenceIfExists(List<string> references, string path)
        {
            if (File.Exists(path) && !references.Contains(path))
            {
                references.Add(path);
            }
        }

        private static string DiagnosticsJson(string compilerOutput)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("[");
            string[] lines = (compilerOutput == null ? string.Empty : compilerOutput).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (builder.Length > 1)
                {
                    builder.Append(",");
                }

                builder.Append("{\"message\":\"");
                builder.Append(JsonTools.Escape(line));
                builder.Append("\"}");
            }

            if (builder.Length == 1 && compilerOutput != null && compilerOutput.Length > 0)
            {
                builder.Append("{\"message\":\"");
                builder.Append(JsonTools.Escape(compilerOutput));
                builder.Append("\"}");
            }

            builder.Append("]");
            return builder.ToString();
        }
    }

    internal sealed class CompileResult
    {
        public Assembly Assembly;
        public SessionUpdate SessionUpdate;
    }

    internal sealed class CompilationException : Exception
    {
        public readonly string DiagnosticsJson;

        public CompilationException(string diagnosticsJson)
            : base("Snippet compilation failed.")
        {
            DiagnosticsJson = diagnosticsJson;
        }
    }

    internal static class RoslynDependencyLoader
    {
        private static bool _installed;

        public static void Install()
        {
            if (_installed)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            _installed = true;
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            AssemblyName name = new AssemblyName(args.Name);
            string path = Path.Combine(Environment.CurrentDirectory, "Mods", "ModTestBridgeLib", name.Name + ".dll");
            if (File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }

            path = Path.Combine(Environment.CurrentDirectory, "Mods", name.Name + ".dll");
            if (File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }

            return null;
        }
    }

    internal static class JsonTools
    {
        public static string FindJsonString(string json, string name)
        {
            Regex regex = new Regex("\"" + Regex.Escape(name) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase);
            Match match = regex.Match(json == null ? string.Empty : json);
            return match.Success ? Unescape(match.Groups[1].Value) : null;
        }

        public static int FindJsonInt(string json, string name, int defaultValue)
        {
            Regex regex = new Regex("\"" + Regex.Escape(name) + "\"\\s*:\\s*([0-9]+)", RegexOptions.IgnoreCase);
            Match match = regex.Match(json == null ? string.Empty : json);
            if (!match.Success)
            {
                return defaultValue;
            }

            int value;
            return int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out value) ? value : defaultValue;
        }

        public static string Escape(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static string Unescape(string value)
        {
            StringBuilder builder = new StringBuilder();
            bool escaped = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!escaped)
                {
                    if (c == '\\')
                    {
                        escaped = true;
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    continue;
                }

                if (c == 'n')
                {
                    builder.Append('\n');
                }
                else if (c == 'r')
                {
                    builder.Append('\r');
                }
                else if (c == 't')
                {
                    builder.Append('\t');
                }
                else if (c == 'u' && i + 4 < value.Length)
                {
                    string hex = value.Substring(i + 1, 4);
                    int codePoint;
                    if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codePoint))
                    {
                        builder.Append((char)codePoint);
                        i += 4;
                    }
                    else
                    {
                        builder.Append(c);
                    }
                }
                else
                {
                    builder.Append(c);
                }

                escaped = false;
            }

            return builder.ToString();
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
