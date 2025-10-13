using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Allumi.WindowsSensor.Models;

namespace Allumi.WindowsSensor.Sync
{
    public sealed class SyncClient
    {
        private readonly HttpClient _http = new();
        private readonly string _deviceId;
        private readonly string _syncUrl;

        public SyncClient(string apiKey, string deviceId, string syncUrl)
        {
            _deviceId = deviceId;
            _syncUrl  = syncUrl;
            _http.Timeout = TimeSpan.FromSeconds(8);

            // Use your device API key from config.json as Bearer (adjust later if your function expects differently)
            if (!string.IsNullOrWhiteSpace(apiKey))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task SendEventAsync(ActivityEvent ev, CancellationToken ct = default)
        {
            var payload = new { device_id = _deviceId, events = new[] { ev } };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var res = await _http.PostAsync(_syncUrl, content, ct);
            res.EnsureSuccessStatusCode();
        }
    }
}
