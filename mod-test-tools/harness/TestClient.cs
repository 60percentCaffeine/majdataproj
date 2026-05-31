using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ModTestHarness
{
    public sealed class TestClient : IDisposable
    {
        private readonly HttpClient _http;

        public TestClient(string host, int port)
        {
            BaseUri = new Uri("http://" + host + ":" + port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(70);
        }

        public Uri BaseUri { get; }

        public Task<BridgeResponse> HealthAsync(CancellationToken cancellationToken = default)
        {
            return GetAsync("/health", cancellationToken);
        }

        public Task<BridgeResponse> EvalAsync(string code, CancellationToken cancellationToken = default)
        {
            return EvalAsync(new EvalRequest { Code = code }, cancellationToken);
        }

        public Task<BridgeResponse> EvalAsync(EvalRequest request, CancellationToken cancellationToken = default)
        {
            return PostJsonAsync("/eval", request, cancellationToken);
        }

        public Task<BridgeResponse> EvalIsolatedAsync(string code, CancellationToken cancellationToken = default)
        {
            return EvalIsolatedAsync(new EvalRequest { Code = code }, cancellationToken);
        }

        public Task<BridgeResponse> EvalIsolatedAsync(EvalRequest request, CancellationToken cancellationToken = default)
        {
            return PostJsonAsync("/eval-isolated", request, cancellationToken);
        }

        public Task<BridgeResponse> ResetSessionAsync(CancellationToken cancellationToken = default)
        {
            return PostJsonAsync("/reset-session", new { }, cancellationToken);
        }

        public Task<BridgeResponse> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            return PostJsonAsync("/shutdown", new { }, cancellationToken);
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        private async Task<BridgeResponse> GetAsync(string path, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await _http.GetAsync(new Uri(BaseUri, path), cancellationToken).ConfigureAwait(false);
            return new BridgeResponse { StatusCode = (int)response.StatusCode, Content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) };
        }

        private async Task<BridgeResponse> PostJsonAsync(string path, object body, CancellationToken cancellationToken)
        {
            string json = JsonSerializer.Serialize(body);
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _http.PostAsync(new Uri(BaseUri, path), content, cancellationToken).ConfigureAwait(false);
            return new BridgeResponse { StatusCode = (int)response.StatusCode, Content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) };
        }
    }
}
