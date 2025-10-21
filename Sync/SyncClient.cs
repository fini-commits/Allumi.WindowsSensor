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
        private readonly HashSet<string> _sentActivityKeys = new(); // Track sent activities to prevent duplicates
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

        // Create unique key for activity deduplication
        private string GetActivityKey(ActivityEvent activity)
        {
            // Round start time to nearest second for consistent key generation
            var startTime = DateTime.Parse(activity.startTime);
            var roundedStart = new DateTime(
                startTime.Year, startTime.Month, startTime.Day,
                startTime.Hour, startTime.Minute, startTime.Second, DateTimeKind.Utc);
            
            return $"{activity.appName}|{activity.windowTitle}|{roundedStart:O}";
        }

        // Check if activity was already sent successfully
        private bool WasAlreadySent(string activityKey)
        {
            lock (_lock)
            {
                return _sentActivityKeys.Contains(activityKey);
            }
        }

        // Mark activity as successfully sent
        private void MarkAsSent(string activityKey)
        {
            lock (_lock)
            {
                _sentActivityKeys.Add(activityKey);
                
                // Keep only last 1000 keys to prevent memory bloat
                if (_sentActivityKeys.Count > 1000)
                {
                    var oldest = _sentActivityKeys.Take(500).ToList();
                    foreach (var key in oldest)
                    {
                        _sentActivityKeys.Remove(key);
                    }
                }
            }
        }

        // Queue activity for batch sending (with deduplication check)
        public void QueueActivity(ActivityEvent ev)
        {
            var key = GetActivityKey(ev);
            
            lock (_lock)
            {
                // Don't queue if already sent
                if (_sentActivityKeys.Contains(key))
                {
                    Log($"[Queue] Skipping duplicate: {ev.appName} - already sent");
                    return;
                }
                
                // Don't queue if already in pending list
                var isDuplicate = _pendingActivities.Any(a => GetActivityKey(a) == key);
                if (isDuplicate)
                {
                    Log($"[Queue] Skipping duplicate: {ev.appName} - already queued");
                    return;
                }
                
                _pendingActivities.Add(ev);
                Log($"[Queue] Added: {ev.appName} ({_pendingActivities.Count} pending)");
            }
        }

        // Send a single activity immediately (real-time sync)
        public async Task<bool> SendActivityImmediatelyAsync(ActivityEvent activity, CancellationToken ct = default)
        {
            var activityKey = GetActivityKey(activity);
            
            // Check if already sent (deduplication)
            if (WasAlreadySent(activityKey))
            {
                Log($"[Instant Sync] Skipping duplicate: {activity.appName} - already sent successfully");
                return true; // Return true because it was already sent
            }
            
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
                    
                    // Queue for retry if server error (with deduplication protection)
                    if ((int)res.StatusCode >= 500)
                    {
                        QueueActivity(activity); // QueueActivity now has deduplication built-in
                        Log("[Instant Sync] Queued for retry (server error)");
                    }
                    return false;
                }

                // SUCCESS - mark as sent to prevent duplicates
                MarkAsSent(activityKey);
                Log($"[Instant Sync] SUCCESS: {activity.appName} ({activity.durationSeconds}s)");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[Instant Sync] EXCEPTION: {ex.GetType().Name} - {ex.Message}");
                Log($"[Instant Sync] Stack trace: {ex.StackTrace}");
                
                // Queue for retry on network errors (with deduplication protection)
                QueueActivity(activity); // QueueActivity now has deduplication built-in
                Log("[Instant Sync] Queued for retry (network error)");
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
                    Log($"[Batch Sync] Failed: {res.StatusCode} - {error}");
                    
                    // Re-queue activities if server error (not auth error)
                    // QueueActivity has built-in deduplication
                    if ((int)res.StatusCode >= 500)
                    {
                        foreach (var activity in toSend)
                        {
                            QueueActivity(activity);
                        }
                        Log($"[Batch Sync] Re-queued {toSend.Count} activities for retry");
                    }
                    return false;
                }

                // SUCCESS - mark all activities as sent
                foreach (var activity in toSend)
                {
                    var key = GetActivityKey(activity);
                    MarkAsSent(key);
                }
                Log($"[Batch Sync] SUCCESS: Sent {toSend.Count} activities");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[Batch Sync] Exception: {ex.Message}");
                // Re-queue on network errors with deduplication
                foreach (var activity in toSend)
                {
                    QueueActivity(activity);
                }
                Log($"[Batch Sync] Re-queued {toSend.Count} activities after exception");
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
