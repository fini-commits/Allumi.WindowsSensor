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
        private readonly string _logPath;

        public SyncClient(string apiKey, string deviceId, string deviceName, string syncUrl)
        {
            _apiKey = apiKey;
            _deviceId = deviceId;
            _deviceName = deviceName;
            _syncUrl = syncUrl;
            _http.Timeout = TimeSpan.FromSeconds(15);

            // Setup logging - use EXE directory (works with Squirrel)
            var exeDir = AppContext.BaseDirectory;
            var logDir = Path.Combine(exeDir, "logs");
            Directory.CreateDirectory(logDir);
            _logPath = Path.Combine(logDir, "sync.log");

            // Note: API key is sent in request body, not as a header
        }

        private void Log(string message)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("O");
                File.AppendAllText(_logPath, $"{timestamp}\t{message}{Environment.NewLine}");
            }
            catch { }
        }

        // Queue activity for batch sending
        public void QueueActivity(ActivityEvent ev)
        {
            lock (_lock)
            {
                _pendingActivities.Add(ev);
            }
        }

        // Send a single activity immediately (real-time sync)
        public async Task<bool> SendActivityImmediatelyAsync(ActivityEvent activity, CancellationToken ct = default)
        {
            try
            {
                Log($"[Instant Sync] Starting sync for {activity.appName} ({activity.durationSeconds}s)");
                
                // Match the working app format: apiKey in body + JWT in Authorization header
                var requestBody = new
                {
                    apiKey = _apiKey,  // Send API key in body (like working app)
                    activities = new[] { new
                    {
                        deviceId = _deviceId,
                        deviceName = _deviceName,
                        appName = activity.appName,
                        windowTitle = activity.windowTitle,
                        startTime = activity.startTime,
                        endTime = activity.endTime,
                        durationSeconds = activity.durationSeconds,
                        category = activity.category,
                        isIdle = activity.isIdle
                    }},
                    deviceInfo = new
                    {
                        osVersion = GetOSVersion(),
                        syncFrequency = 60
                    }
                };

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });

                Log($"[Instant Sync] Request JSON: {json.Substring(0, Math.Min(200, json.Length))}...");
                Log($"[Instant Sync] Posting to: {_syncUrl}");
                Log($"[Instant Sync] Using apiKey in body: {_apiKey.Substring(0, 10)}...");

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var res = await _http.PostAsync(_syncUrl, content, ct);
                
                if (!res.IsSuccessStatusCode)
                {
                    var error = await res.Content.ReadAsStringAsync(ct);
                    Log($"[Instant Sync] FAILED: {res.StatusCode} - {error}");
                    
                    // DON'T queue for retry - this can cause duplicates
                    // Let the activity be lost rather than creating duplicates
                    // The next activity will be tracked normally
                    Log("[Instant Sync] Activity dropped (no retry to prevent duplicates)");
                    return false;
                }

                Log($"[Instant Sync] SUCCESS: {activity.appName} ({activity.durationSeconds}s)");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[Instant Sync] EXCEPTION: {ex.GetType().Name} - {ex.Message}");
                Log($"[Instant Sync] Stack trace: {ex.StackTrace}");
                // DON'T queue for retry - this can cause duplicates
                // Let the activity be lost rather than creating duplicates
                Log("[Instant Sync] Activity dropped (no retry to prevent duplicates)");
                return false;
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
                var requestBody = new SyncActivityRequest
                {
                    activities = toSend,
                    deviceInfo = new DeviceInfo
                    {
                        deviceName = _deviceName,
                        deviceType = "desktop",
                        osVersion = GetOSVersion()
                    }
                };

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, _syncUrl);
                request.Content = content;
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");
                
                using var res = await _http.SendAsync(request, ct);
                
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
