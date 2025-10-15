using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Allumi.WindowsSensor.Models;

namespace Allumi.WindowsSensor.Sync
{
    public sealed class SyncClient
    {
        private readonly HttpClient _http = new();
        private readonly string _apiKey;
        private readonly string _deviceId;
        private readonly string _deviceName;
        private readonly string _syncUrl;
        private readonly List<ActivityEvent> _pendingActivities = new();
        private readonly object _lock = new();

        public SyncClient(string apiKey, string deviceId, string deviceName, string syncUrl)
        {
            _apiKey = apiKey;
            _deviceId = deviceId;
            _deviceName = deviceName;
            _syncUrl = syncUrl;
            _http.Timeout = TimeSpan.FromSeconds(15);

            // Note: API key is sent in request body, not as a header
        }

        // Queue activity for batch sending
        public void QueueActivity(ActivityEvent ev)
        {
            lock (_lock)
            {
                _pendingActivities.Add(ev);
            }
        }

        // Send batched activities to Vetra
        public async Task<bool> FlushActivitiesAsync(CancellationToken ct = default)
        {
            List<ActivityEvent> toSend;
            lock (_lock)
            {
                if (_pendingActivities.Count == 0)
                    return true;

                toSend = new List<ActivityEvent>(_pendingActivities);
                _pendingActivities.Clear();
            }

            try
            {
                var request = new SyncActivityRequest
                {
                    apiKey = _apiKey,
                    activities = toSend,
                    deviceInfo = new DeviceInfo
                    {
                        deviceName = _deviceName,
                        deviceType = "desktop",
                        osVersion = GetOSVersion()
                    }
                };

                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var res = await _http.PostAsync(_syncUrl, content, ct);
                
                if (!res.IsSuccessStatusCode)
                {
                    var error = await res.Content.ReadAsStringAsync(ct);
                    Console.WriteLine($"Sync failed: {res.StatusCode} - {error}");
                    
                    // Re-queue activities if server error (not auth error)
                    if ((int)res.StatusCode >= 500)
                    {
                        lock (_lock)
                        {
                            _pendingActivities.InsertRange(0, toSend);
                        }
                    }
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sync exception: {ex.Message}");
                // Re-queue on network errors
                lock (_lock)
                {
                    _pendingActivities.InsertRange(0, toSend);
                }
                return false;
            }
        }

        private static string GetOSVersion()
        {
            try
            {
                return $"Windows {Environment.OSVersion.Version}";
            }
            catch
            {
                return "Windows";
            }
        }
    }
}
