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

            // PRIORITY 1: Try to load embedded config (from custom installer)
            string? embeddedConfig = EmbeddedConfigReader.ExtractEmbeddedConfig();
            if (embeddedConfig != null)
            {
                try
                {
                    var cfg = JsonSerializer.Deserialize<AppConfig>(embeddedConfig) ?? new AppConfig();
                    
                    // Save embedded config to file system for future use
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(path, embeddedConfig);
                    
                    // Store API key securely in Windows Credential Manager
                    if (!string.IsNullOrWhiteSpace(cfg.apiKey))
                    {
                        CredentialManager.SaveApiKey(cfg.apiKey);
                        Console.WriteLine("API key stored securely in Windows Credential Manager");
                    }
                    
                    Console.WriteLine($"Loaded embedded config and saved to: {path}");
                    return (cfg, "embedded");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to parse embedded config: {ex.Message}");
                    // Fall through to try file system
                }
            }

            // PRIORITY 2: Try to load from file system
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
                
                // Try to load API key from Credential Manager first
                var storedApiKey = CredentialManager.GetApiKey();
                if (!string.IsNullOrWhiteSpace(storedApiKey))
                {
                    cfg.apiKey = storedApiKey;
                    Console.WriteLine("Loaded API key from Windows Credential Manager");
                }
                
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
