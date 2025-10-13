namespace Allumi.WindowsSensor.Models
{
    public sealed class ActivityEvent
    {
        public string device_id { get; set; } = "";
        public string app_name { get; set; } = "";
        public string window_title { get; set; } = "";
        public string status { get; set; } = "active";   // active|idle|locked
        public System.DateTime started_at { get; set; }
        public System.DateTime ended_at { get; set; }
        public int duration_seconds { get; set; }
    }
}
