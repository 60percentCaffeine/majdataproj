using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ModTestReplClient
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            BridgeEndpoint endpoint = BridgeEndpoint.FromArgs(args);
            Console.Title = "ModTestBridge REPL " + endpoint.BaseUrl;
            Console.WriteLine("ModTestBridge REPL connected to " + endpoint.BaseUrl);
            Console.WriteLine("Type :help for commands.");

            while (true)
            {
                Console.Write("> ");
                string line = Console.ReadLine();
                if (line == null)
                {
                    return 0;
                }

                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == ':')
                {
                    if (!HandleCommand(endpoint, line))
                    {
                        return 0;
                    }

                    continue;
                }

                try
                {
                    Console.WriteLine(PostEval(endpoint, "/eval", line));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                }
            }
        }

        private static bool HandleCommand(BridgeEndpoint endpoint, string command)
        {
            switch (command.Trim())
            {
                case ":exit":
                    return false;
                case ":clear":
                    Console.Clear();
                    return true;
                case ":help":
                    Console.WriteLine(":reset clears the persistent bridge session");
                    Console.WriteLine(":clear clears this console");
                    Console.WriteLine(":exit exits the client");
                    Console.WriteLine(":help shows commands");
                    return true;
                case ":reset":
                    Console.WriteLine(Post(endpoint, "/reset-session", "{}"));
                    return true;
                default:
                    Console.WriteLine("Unknown command. Type :help.");
                    return true;
            }
        }

        private static string PostEval(BridgeEndpoint endpoint, string path, string code)
        {
            string body = JsonSerializer.Serialize(new EvalRequest
            {
                code = code,
                timeoutMs = 60000,
                maxDepth = 6,
                maxResponseBytes = 262144
            });
            return Post(endpoint, path, body);
        }

        private static string Post(BridgeEndpoint endpoint, string path, string body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint.BaseUrl + path);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = bytes.Length;
            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response == null)
                {
                    throw;
                }

                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private sealed class EvalRequest
        {
            public string code { get; set; }
            public int timeoutMs { get; set; }
            public int maxDepth { get; set; }
            public int maxResponseBytes { get; set; }
        }
    }

    internal sealed class BridgeEndpoint
    {
        public string Host = "127.0.0.1";
        public int Port = 17443;

        public string BaseUrl
        {
            get { return "http://" + Host + ":" + Port.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        }

        public static BridgeEndpoint FromArgs(string[] args)
        {
            BridgeEndpoint endpoint = new BridgeEndpoint();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--host" && i + 1 < args.Length)
                {
                    endpoint.Host = args[++i];
                }
                else if (args[i] == "--port" && i + 1 < args.Length)
                {
                    int.TryParse(args[++i], out endpoint.Port);
                }
            }

            return endpoint;
        }
    }
}
