using System.Text.Json;
using Allumi.WindowsSensor.Auth;

namespace Allumi.WindowsSensor
{
    public sealed class AppConfig
    {
        public string deviceId { get; set; } = "";
        public string deviceName { get; set; } = "";
        public string apiKey   { get; set; } = "";  // device-scoped key
        public string syncUrl  { get; set; } = "";
        public string? userId  { get; set; }

        // optional: if your Edge Function requires the Supabase anon key in Authorization
        public string? supabaseAnonKey { get; set; }
        public string? supabaseUrl { get; set; }
    }

    internal static class Config
    {
        public static (AppConfig cfg, string sourcePath) Load()
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(roaming, "Allumi");
            var path = Path.Combine(dir, "config.json");

            // DISABLED: Embedded config extraction (was overwriting manual config updates)
            // string? embeddedConfig = EmbeddedConfigReader.ExtractEmbeddedConfig();
            // if (embeddedConfig != null) { ... }

            // PRIORITY 1: Try to load from file system
            if (!File.Exists(path))
            {
                // Fallback: next to EXE (for dev)
                path = Path.Combine(AppContext.BaseDirectory, "config.json");
            }

            if (!File.Exists(path))
            {
                Console.WriteLine("No config found. User needs to authenticate.");
                return (new AppConfig(), path); // empty cfg + the last-checked path
            }

            try
            {
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                
                // DISABLED: Credential Manager (was caching old API keys)
                // var storedApiKey = CredentialManager.GetApiKey();
                // if (!string.IsNullOrWhiteSpace(storedApiKey)) { cfg.apiKey = storedApiKey; }
                
                return (cfg, path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load config from {path}: {ex.Message}");
                return (new AppConfig(), path);
            }
        }
    }
}
