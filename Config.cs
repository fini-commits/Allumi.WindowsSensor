using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Json;
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
        public int idleThresholdSeconds { get; set; } = 60;

        // optional: if your Edge Function requires the Supabase anon key in Authorization
        public string? supabaseAnonKey { get; set; }
        public string? supabaseUrl { get; set; }
    }

    public sealed class TokenExchangeRequest
    {
        public string token { get; set; } = "";
    }

    public sealed class TokenExchangeResponse
    {
        public string deviceId { get; set; } = "";
        public string deviceName { get; set; } = "";
        public string apiKey { get; set; } = "";
        public string syncUrl { get; set; } = "";
        public string userId { get; set; } = "";
        public int idleThresholdSeconds { get; set; } = 60;
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

            // PRIORITY 1: Check if config file exists
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                    return (cfg, path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load config from {path}: {ex.Message}");
                }
            }

            // PRIORITY 2: Try token exchange (for first-time install)
            var token = GetExchangeToken();
            if (!string.IsNullOrEmpty(token))
            {
                Console.WriteLine($"Found exchange token, attempting to exchange for credentials...");
                var exchangedConfig = ExchangeTokenForConfig(token);
                if (exchangedConfig != null)
                {
                    Console.WriteLine("Token exchange successful! Saving config...");
                    var configJson = JsonSerializer.Serialize(exchangedConfig, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    Save(configJson);
                    return (exchangedConfig, path);
                }
                else
                {
                    Console.WriteLine("Token exchange failed or token expired.");
                }
            }

            // PRIORITY 3: No config and no token - need OAuth
            Console.WriteLine($"No config found at {path}. User needs to authenticate.");
            return (new AppConfig(), path);
        }

        private static string? GetExchangeToken()
        {
            try
            {
                // Check for token file created by installer
                var exeDir = AppContext.BaseDirectory;
                var tokenPath = Path.Combine(exeDir, ".exchange-token");
                
                if (File.Exists(tokenPath))
                {
                    var token = File.ReadAllText(tokenPath).Trim();
                    Console.WriteLine($"Found exchange token file: {tokenPath}");
                    
                    // Delete token file after reading (single-use)
                    try { File.Delete(tokenPath); } catch { }
                    
                    return token;
                }

                // Check environment variable (alternative method)
                var envToken = Environment.GetEnvironmentVariable("ALLUMI_EXCHANGE_TOKEN");
                if (!string.IsNullOrEmpty(envToken))
                {
                    Console.WriteLine("Found exchange token in environment variable");
                    return envToken;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading exchange token: {ex.Message}");
                return null;
            }
        }

        private static AppConfig? ExchangeTokenForConfig(string token)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var exchangeUrl = "https://lstannxhfhunacgkvtmm.supabase.co/functions/v1/exchange-device-token";
                
                var request = new TokenExchangeRequest { token = token };
                var response = httpClient.PostAsJsonAsync(exchangeUrl, request).Result;

                if (response.IsSuccessStatusCode)
                {
                    var result = response.Content.ReadFromJsonAsync<TokenExchangeResponse>().Result;
                    if (result != null)
                    {
                        return new AppConfig
                        {
                            deviceId = result.deviceId,
                            deviceName = result.deviceName,
                            apiKey = result.apiKey,
                            syncUrl = result.syncUrl,
                            userId = result.userId,
                            idleThresholdSeconds = result.idleThresholdSeconds
                        };
                    }
                }
                else
                {
                    var error = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine($"Token exchange failed: {response.StatusCode} - {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during token exchange: {ex.Message}");
            }

            return null;
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
