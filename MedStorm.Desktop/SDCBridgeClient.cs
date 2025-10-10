using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MedStorm.Desktop.Sdc
{
    public sealed class SdcBridgeClient : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly HttpClient _http;
        private readonly Uri _postUri;
        private readonly bool _enabled;

        public SdcBridgeClient(string baseUrl, int timeoutMs, bool enabled = true)
        {
            _enabled = enabled;
            _http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs <= 0 ? 500 : timeoutMs) };
            _postUri = new Uri(new Uri(baseUrl.TrimEnd('/')), "/metrics");
        }

        public void Dispose() => _http?.Dispose();

        public void TryPost(object payload)
        {
            if (!_enabled) return;
            _ = PostInternalAsync(payload);
        }

        private async Task PostInternalAsync(object payload)
        {
            try
            {
                using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
                using var res = await _http.PostAsync(_postUri, content).ConfigureAwait(false);
                // 2xx -> ok, else log once (avoid UI impact)
                if (!res.IsSuccessStatusCode)
                {
                    var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    System.Diagnostics.Trace.TraceWarning($"SDC POST failed: {(int)res.StatusCode} {res.ReasonPhrase} {body}");
                }
            }
            catch (Exception ex)
            {
                // swallow (non-fatal) but trace for support
                System.Diagnostics.Trace.TraceWarning($"SDC POST exception: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
