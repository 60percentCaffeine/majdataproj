using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

    public sealed class EvalGlobals
    {
        public string BridgeVersion
        {
            get { return ModTestBridgeMod.BridgeVersion; }
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

        public EvalDispatcher(SynchronizationContext mainThreadContext)
        {
            _mainThreadContext = mainThreadContext;
        }

        public EvalResponse EvaluateIsolated(EvalRequest request)
        {
            EvalWorkItem item = new EvalWorkItem(request, _evalService);
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
    }

    internal sealed class EvalWorkItem
    {
        private readonly EvalRequest _request;
        private readonly RoslynEvalService _evalService;
        private readonly ManualResetEvent _done = new ManualResetEvent(false);

        public EvalResponse Response;

        public EvalWorkItem(EvalRequest request, RoslynEvalService evalService)
        {
            _request = request;
            _evalService = evalService;
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
                Task<object> task = _evalService.StartIsolated(_request);
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
                        Response = EvalResponse.Success(completed.Result);
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

        public static EvalResponse Success(object value)
        {
            return new EvalResponse
            {
                StatusCode = 200,
                Json = "{\"ok\":true,\"phase\":\"execution\",\"result\":" + JsonTools.SerializeSimpleValue(value) + "}"
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

    internal sealed class RoslynEvalService
    {
        public Task<object> StartIsolated(EvalRequest request)
        {
            Assembly assembly = Compile(request.Code);
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

        private Assembly Compile(string code)
        {
            CompilationException statementFailure;
            try
            {
                return CompileWrapped(ExpressionBody(code));
            }
            catch (CompilationException ex)
            {
                statementFailure = ex;
            }

            try
            {
                return CompileWrapped(code + "\r\nreturn Task.FromResult<object>(null);");
            }
            catch (CompilationException)
            {
                throw statementFailure;
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

        private Assembly CompileWrapped(string body)
        {
            string source = ""
                + "using System;\r\n"
                + "using System.Linq;\r\n"
                + "using System.Collections.Generic;\r\n"
                + "using System.Reflection;\r\n"
                + "using System.Threading.Tasks;\r\n"
                + "using MelonLoader;\r\n"
                + "using UnityEngine;\r\n"
                + "using ModTestBridge;\r\n"
                + "namespace __ModTestBridgeEval {\r\n"
                + "  public static class Snippet {\r\n"
                + "    public static Task<object> Run(EvalGlobals globals) {\r\n"
                + body + "\r\n"
                + "    }\r\n"
                + "  }\r\n"
                + "}\r\n";

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

            string bridgeAssemblyPath = Path.Combine(Environment.CurrentDirectory, "Mods", "ModTestBridge.dll");
            if (File.Exists(bridgeAssemblyPath) && !references.Contains(bridgeAssemblyPath))
            {
                references.Add(bridgeAssemblyPath);
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

        public static string SerializeSimpleValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is string)
            {
                return "\"" + Escape((string)value) + "\"";
            }

            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }

            if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return "{\"type\":\"" + Escape(value.GetType().FullName) + "\",\"string\":\"" + Escape(value.ToString()) + "\"}";
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
