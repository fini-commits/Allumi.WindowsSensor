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
        private static string GetConfigPath()
        {
            // PRIORITY 1: Next to EXE (works for both dev and Squirrel installed app)
            // Squirrel installs to: %LocalAppData%\AllumiWindowsSensor\app-x.x.x\
            var exeDir = AppContext.BaseDirectory;
            var path = Path.Combine(exeDir, "config.json");
            
            if (File.Exists(path))
                return path;
            
            // PRIORITY 2: Legacy location for existing users (AppData\Roaming\Allumi)
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var legacyDir = Path.Combine(roaming, "Allumi");
            var legacyPath = Path.Combine(legacyDir, "config.json");
            
            if (File.Exists(legacyPath))
                return legacyPath;
            
            // Default: use EXE directory (for new installs)
            return path;
        }

        public static (AppConfig cfg, string sourcePath) Load()
        {
            var path = GetConfigPath();

            if (!File.Exists(path))
            {
                Console.WriteLine($"No config found at {path}. User needs to authenticate.");
                return (new AppConfig(), path);
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

        public static bool Save(string configJson)
        {
            try
            {
                // Save to EXE directory (works for both dev and Squirrel)
                var exeDir = AppContext.BaseDirectory;
                var path = Path.Combine(exeDir, "config.json");
                
                // Validate it's valid JSON by trying to parse it
                var cfg = JsonSerializer.Deserialize<AppConfig>(configJson);
                if (cfg == null)
                {
                    Console.WriteLine("Invalid config JSON received");
                    return false;
                }
                
                // Save the config
                File.WriteAllText(path, configJson);
                Console.WriteLine($"Config saved to {path}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save config: {ex.Message}");
                return false;
            }
        }
    }
}
