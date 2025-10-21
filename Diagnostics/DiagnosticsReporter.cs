using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Allumi.WindowsSensor.Diagnostics
{
    /// <summary>
    /// Collects and reports diagnostic information to the backend for remote debugging
    /// </summary>
    public class DiagnosticsReporter
    {
        private readonly string _apiUrl;
        private readonly string _deviceId;
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public DiagnosticsReporter(string apiUrl, string deviceId, string apiKey)
        {
            _apiUrl = apiUrl;
            _deviceId = deviceId;
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        public async Task ReportHealthCheckAsync()
        {
            try
            {
                var diagnostics = CollectDiagnostics();
                var json = JsonSerializer.Serialize(diagnostics);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_apiUrl}/report-diagnostics", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to report diagnostics: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reporting diagnostics: {ex.Message}");
            }
        }

        private DiagnosticsData CollectDiagnostics()
        {
            return new DiagnosticsData
            {
                DeviceId = _deviceId,
                AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                OS = Environment.OSVersion.ToString(),
                OSVersion = Environment.OSVersion.Version.ToString(),
                DotNetVersion = Environment.Version.ToString(),
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Process.GetCurrentProcess().WorkingSet64,
                MemoryUsageMB = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024,
                ThreadCount = Process.GetCurrentProcess().Threads.Count,
                HandleCount = Process.GetCurrentProcess().HandleCount,
                UptimeSeconds = (int)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds,
                LastSyncTime = DateTime.UtcNow, // Will be updated by caller
                ConfigExists = File.Exists(Path.Combine(AppContext.BaseDirectory, "config.json")),
                LogFileSize = GetLogFileSize(),
                ErrorCount = GetErrorCount(),
                LastError = GetLastError(),
                Timestamp = DateTime.UtcNow
            };
        }

        private long GetLogFileSize()
        {
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "sensor.log");
                return File.Exists(logPath) ? new FileInfo(logPath).Length : 0;
            }
            catch { return 0; }
        }

        private int GetErrorCount()
        {
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "sensor.log");
                if (!File.Exists(logPath)) return 0;
                
                var lines = File.ReadAllLines(logPath);
                int count = 0;
                foreach (var line in lines)
                {
                    if (line.Contains("ERROR") || line.Contains("FAILED") || line.Contains("Exception"))
                        count++;
                }
                return count;
            }
            catch { return 0; }
        }

        private string? GetLastError()
        {
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "sensor.log");
                if (!File.Exists(logPath)) return null;
                
                var lines = File.ReadAllLines(logPath);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    if (lines[i].Contains("ERROR") || lines[i].Contains("FAILED") || lines[i].Contains("Exception"))
                        return lines[i];
                }
                return null;
            }
            catch { return null; }
        }
    }

    public class DiagnosticsData
    {
        public string DeviceId { get; set; } = "";
        public string AppVersion { get; set; } = "";
        public string OS { get; set; } = "";
        public string OSVersion { get; set; } = "";
        public string DotNetVersion { get; set; } = "";
        public string MachineName { get; set; } = "";
        public string UserName { get; set; } = "";
        public int ProcessorCount { get; set; }
        public long WorkingSet { get; set; }
        public long MemoryUsageMB { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public int UptimeSeconds { get; set; }
        public DateTime LastSyncTime { get; set; }
        public bool ConfigExists { get; set; }
        public long LogFileSize { get; set; }
        public int ErrorCount { get; set; }
        public string? LastError { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
