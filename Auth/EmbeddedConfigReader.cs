using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Allumi.WindowsSensor.Auth
{
    /// <summary>
    /// Reads configuration JSON embedded in the executable by the installer generator
    /// </summary>
    public static class EmbeddedConfigReader
    {
        private const string CONFIG_START_MARKER = "<<<ALLUMI_CONFIG>>>";
        private const string CONFIG_END_MARKER = "<<<END_CONFIG>>>";

        /// <summary>
        /// Attempts to extract embedded configuration from the current executable
        /// </summary>
        /// <returns>Config JSON string if found, null otherwise</returns>
        public static string? ExtractEmbeddedConfig()
        {
            try
            {
                // First, try reading from the current executable
                var exePath = Assembly.GetExecutingAssembly().Location;
                var configJson = TryExtractFromFile(exePath);
                
                if (configJson != null)
                {
                    Console.WriteLine("Found embedded config in current executable");
                    return configJson;
                }
                
                // If not found, try looking for the Setup.exe in common locations
                // This handles the case where Squirrel extracts the app but config is in Setup.exe
                var possibleSetupPaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Setup.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AllumiWindowsSensor", "Setup.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AllumiTracker_Setup.exe")
                };
                
                foreach (var setupPath in possibleSetupPaths)
                {
                    if (File.Exists(setupPath))
                    {
                        Console.WriteLine($"Checking for embedded config in: {setupPath}");
                        configJson = TryExtractFromFile(setupPath);
                        if (configJson != null)
                        {
                            Console.WriteLine("Found embedded config in Setup.exe");
                            return configJson;
                        }
                    }
                }
                
                Console.WriteLine("No embedded config found in any location");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting embedded config: {ex.Message}");
                return null;
            }
        }
        
        private static string? TryExtractFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                // Read the entire executable as bytes
                byte[] exeBytes = File.ReadAllBytes(filePath);
                
                // Convert to string for marker search (UTF-8)
                string exeText = Encoding.UTF8.GetString(exeBytes);
                
                // Find the markers
                int startIdx = exeText.LastIndexOf(CONFIG_START_MARKER);
                int endIdx = exeText.LastIndexOf(CONFIG_END_MARKER);
                
                if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
                {
                    return null;
                }
                
                // Extract the JSON between markers
                int configStart = startIdx + CONFIG_START_MARKER.Length;
                int configLength = endIdx - configStart;
                
                if (configLength <= 0)
                {
                    return null;
                }
                
                string configJson = exeText.Substring(configStart, configLength).Trim();
                
                // Validate it's actually JSON
                try
                {
                    using var doc = JsonDocument.Parse(configJson);
                    return configJson;
                }
                catch (JsonException)
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
