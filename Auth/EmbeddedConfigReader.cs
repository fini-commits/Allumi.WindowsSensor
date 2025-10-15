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
                // Get the path to the currently executing assembly
                var exePath = Assembly.GetExecutingAssembly().Location;
                
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    Console.WriteLine("Could not locate executable path");
                    return null;
                }

                // Read the entire executable as bytes
                byte[] exeBytes = File.ReadAllBytes(exePath);
                
                // Convert to string for marker search (UTF-8)
                string exeText = Encoding.UTF8.GetString(exeBytes);
                
                // Find the markers
                int startIdx = exeText.LastIndexOf(CONFIG_START_MARKER);
                int endIdx = exeText.LastIndexOf(CONFIG_END_MARKER);
                
                if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
                {
                    Console.WriteLine("No embedded config markers found");
                    return null;
                }
                
                // Extract the JSON between markers
                int configStart = startIdx + CONFIG_START_MARKER.Length;
                int configLength = endIdx - configStart;
                
                if (configLength <= 0)
                {
                    Console.WriteLine("Empty config section");
                    return null;
                }
                
                string configJson = exeText.Substring(configStart, configLength).Trim();
                
                // Validate it's actually JSON
                try
                {
                    using var doc = JsonDocument.Parse(configJson);
                    Console.WriteLine("Successfully extracted embedded config");
                    return configJson;
                }
                catch (JsonException)
                {
                    Console.WriteLine("Embedded data is not valid JSON");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting embedded config: {ex.Message}");
                return null;
            }
        }
    }
}
