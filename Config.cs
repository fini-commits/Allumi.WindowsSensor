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
    public DateTime? policyAgreedAt { get; set; } // UTC timestamp when user agreed to privacy policy

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
                    
                    // Delete token file ONLY after successful exchange
                    DeleteExchangeToken();
                    
                    return (exchangedConfig, path);
                }
                else
                {
                    Console.WriteLine("Token exchange failed or token expired. Token file preserved for retry.");
                    // DON'T delete token - keep it for next retry
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
                    
                    // DON'T delete token here - delete only after successful exchange
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

        private static void DeleteExchangeToken()
        {
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var tokenPath = Path.Combine(exeDir, ".exchange-token");
                
                if (File.Exists(tokenPath))
                {
                    File.Delete(tokenPath);
                    Console.WriteLine("Exchange token file deleted after successful exchange");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting exchange token: {ex.Message}");
            }
        }

        private static AppConfig? ExchangeTokenForConfig(string token)
        {
            try
            {
                Console.WriteLine($"[TOKEN EXCHANGE] Starting exchange for token: {token.Substring(0, Math.Min(20, token.Length))}...");
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                // Add Supabase anon key header (required for edge functions)
                httpClient.DefaultRequestHeaders.Add("apikey", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImxzdGFubnhoZmh1bmFjZ2t2dG1tIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NDk3MTU5MzYsImV4cCI6MjA2NTI5MTkzNn0.4NFI9C88sQOMzvcvYuTNF8MWSq-1vWESF-HoOUhrVS0");

                var exchangeUrl = "https://lstannxhfhunacgkvtmm.supabase.co/functions/v1/exchange-device-token";
                Console.WriteLine($"[TOKEN EXCHANGE] Calling endpoint: {exchangeUrl}");
                
                var request = new TokenExchangeRequest { token = token };
                var response = httpClient.PostAsJsonAsync(exchangeUrl, request).Result;

                Console.WriteLine($"[TOKEN EXCHANGE] Response status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine($"[TOKEN EXCHANGE] Success response: {responseBody}");
                    
                    var result = JsonSerializer.Deserialize<TokenExchangeResponse>(responseBody);
                    if (result != null)
                    {
                        Console.WriteLine($"[TOKEN EXCHANGE] Received deviceId: {result.deviceId}, userId: {result.userId}");
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
                    else
                    {
                        Console.WriteLine($"[TOKEN EXCHANGE] ERROR: Failed to deserialize response");
                    }
                }
                else
                {
                    var error = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine($"[TOKEN EXCHANGE] ERROR: {response.StatusCode} - {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TOKEN EXCHANGE] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[TOKEN EXCHANGE] Stack trace: {ex.StackTrace}");
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
