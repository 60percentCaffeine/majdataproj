using System.Text.Json.Serialization;

namespace ModTestHarness
{
    public sealed class BridgeReadyFile
    {
        [JsonPropertyName("pid")]
        public int Pid { get; set; }

        [JsonPropertyName("host")]
        public string Host { get; set; }

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("startedAt")]
        public string StartedAt { get; set; }

        [JsonPropertyName("bridgeVersion")]
        public string BridgeVersion { get; set; }
    }

    public sealed class EvalRequest
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("timeoutMs")]
        public int TimeoutMs { get; set; } = 60000;

        [JsonPropertyName("maxDepth")]
        public int MaxDepth { get; set; } = 6;

        [JsonPropertyName("maxResponseBytes")]
        public int MaxResponseBytes { get; set; } = 262144;
    }

    public sealed class BridgeResponse
    {
        public int StatusCode { get; set; }
        public string Content { get; set; }
    }
}
