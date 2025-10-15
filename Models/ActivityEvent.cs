namespace Allumi.WindowsSensor.Models
{
    public sealed class ActivityEvent
    {
        // Required fields matching device_activities table structure
        public string appName { get; set; } = "";
        public string windowTitle { get; set; } = "";
        public string startTime { get; set; } = "";      // ISO 8601 format
        public string endTime { get; set; } = "";        // ISO 8601 format
        public int durationSeconds { get; set; }
        public string category { get; set; } = "other";  // Default, AI will categorize later
        public bool isIdle { get; set; } = false;
        
        // Optional fields for AI categorization (populated by backend)
        // These will be null when sent from app, filled by AI later:
        // - ai_subcategory
        // - ai_confidence
        // - ai_reasoning
        // - productivity_score
    }

    // Request wrapper for /sync-device-activity endpoint
    public sealed class SyncActivityRequest
    {
        public string apiKey { get; set; } = "";
        public List<ActivityEvent> activities { get; set; } = new();
        public DeviceInfo deviceInfo { get; set; } = new();
    }

    public sealed class DeviceInfo
    {
        public string deviceName { get; set; } = "";
        public string deviceType { get; set; } = "desktop";
        public string osVersion { get; set; } = "";
    }
}
