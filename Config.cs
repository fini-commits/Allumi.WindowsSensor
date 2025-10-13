using System.Text.Json;

namespace Allumi.WindowsSensor
{
    public sealed class AppConfig
    {
        public string deviceId { get; set; } = "";
        public string apiKey   { get; set; } = "";  // device-scoped key
        public string syncUrl  { get; set; } = "";
        public string? userId  { get; set; }

        // optional: if your Edge Function requires the Supabase anon key in Authorization
        public string? supabaseAnonKey { get; set; }
    }

    internal static class Config
    {
        public static (AppConfig cfg, string sourcePath) Load()
        {
            // 1) %AppData%\Allumi\config.json
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(roaming, "Allumi", "config.json");

            // 2) Fallback: next to EXE (for dev)
            if (!File.Exists(path))
                path = Path.Combine(AppContext.BaseDirectory, "config.json");

            if (!File.Exists(path))
                return (new AppConfig(), path); // empty cfg + the last-checked path

            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            return (cfg, path);
        }
    }
}
