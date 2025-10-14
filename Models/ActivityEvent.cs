namespace Allumi.WindowsSensor.Models
{
    public sealed class ActivityEvent
    {
        // Required fields for Vetra API
        public string deviceId { get; set; } = "";
        public string deviceName { get; set; } = "";
        public string appName { get; set; } = "";
        public string windowTitle { get; set; } = "";
        public string startTime { get; set; } = "";  // ISO 8601 format
        public string endTime { get; set; } = "";    // ISO 8601 format
        public int durationSeconds { get; set; }
        public string category { get; set; } = "other";  // AI will categorize
        public bool isIdle { get; set; } = false;
    }

    // Request wrapper for batch sync
    public sealed class SyncActivityRequest
    {
        public string apiKey { get; set; } = "";
        public List<ActivityEvent> activities { get; set; } = new();
        public DeviceInfo deviceInfo { get; set; } = new();
    }

    public sealed class DeviceInfo
    {
        public string osVersion { get; set; } = "";
        public int syncFrequency { get; set; } = 60;
    }
}
