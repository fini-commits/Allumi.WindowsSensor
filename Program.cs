using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using System.Reflection;
using System.Web;
using Allumi.WindowsSensor.Models;
using Allumi.WindowsSensor.Sync;
using Allumi.WindowsSensor.Update;
using Allumi.WindowsSensor.Auth;
using Allumi.WindowsSensor.Categorization;

namespace Allumi.WindowsSensor
{
    internal static class Program
    {
        public static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        internal static Mutex? _singleInstanceMutex;
        
        [STAThread]
        static void Main(string[] args)
        {
            // Ensure only one instance runs at a time
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, "AllumiWindowsSensor_SingleInstance", out createdNew);
            
            if (!createdNew)
            {
                // Another instance is already running - just exit silently
                // NOTE: Cannot use MessageBox here - would create window before ApplicationConfiguration.Initialize()
                return;
            }
            
            // DEBUG: Log all arguments
            var debugLog = Path.Combine(AppContext.BaseDirectory, "logs", "debug.log");
            try
            {
                var logDir = Path.GetDirectoryName(debugLog);
                if (!string.IsNullOrEmpty(logDir))
                    Directory.CreateDirectory(logDir);
                File.AppendAllText(debugLog, $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Main() called with {args.Length} args\n");
                for (int i = 0; i < args.Length; i++)
                {
                    File.AppendAllText(debugLog, $"  args[{i}] = {args[i]}\n");
                    File.AppendAllText(debugLog, $"  Length: {args[i].Length}, Starts with 'allumi://': {args[i].StartsWith("allumi://", StringComparison.OrdinalIgnoreCase)}\n");
                }
            }
            catch { }

            // Handle Squirrel installation events
            UpdateHelper.HandleSquirrelEvents();
            
            // Register OAuth protocol handler (allumi://)
            OAuthHandler.RegisterProtocolHandler();
            
            // On first run after install, show onboarding window and require privacy agreement
            if (args.Length > 0 && args[0] == "--squirrel-firstrun")
            {
                try { File.AppendAllText(debugLog, "  First run detected - showing onboarding window\n"); } catch { }
                
                // CRITICAL: Must call ApplicationConfiguration.Initialize() BEFORE creating any forms!
                ApplicationConfiguration.Initialize();
                
                var onboarding = new InstallerOnboardingForm();
                var result = onboarding.ShowDialog();
                if (result == DialogResult.OK && onboarding.PolicyAgreed)
                {
                    // Save agreement timestamp to config
                    var (cfg, cfgPath) = Config.Load();
                    cfg.policyAgreedAt = DateTime.UtcNow;
                    var configJson = System.Text.Json.JsonSerializer.Serialize(cfg, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    Config.Save(configJson);
                    
                    // Launch the app (but skip ApplicationConfiguration.Initialize() since we already called it)
                    Application.Run(new TrayApp());
                }
                // If not agreed, just exit
                return;
            }
            
            // Add to Windows startup
            AddToStartup();
            
            // Check if launched via allumi:// protocol
            if (args.Length > 0 && args[0].StartsWith("allumi://", StringComparison.OrdinalIgnoreCase))
            {
                File.AppendAllText(debugLog, $"  Detected allumi:// URL\n");
                
                // Check for setup token (allumi://setup?token=xxx or allumi://setup/?token=xxx)
                if (args[0].Contains("/setup", StringComparison.OrdinalIgnoreCase) && args[0].Contains("token=", StringComparison.OrdinalIgnoreCase))
                {
                    File.AppendAllText(debugLog, $"  Calling HandleSetupToken()\n");
                    HandleSetupToken(args[0]);
                    return; // Exit after saving token
                }
                
                File.AppendAllText(debugLog, $"  Not a setup URL, calling OAuth handler\n");
                // OAuth callback (allumi://auth?token=xxx)
                try
                {
                    OAuthHandler.HandleProtocolCallback(args[0]);
                    File.AppendAllText(debugLog, $"  OAuth callback handled successfully\n");
                }
                catch (Exception ex)
                {
                    File.AppendAllText(debugLog, $"  ERROR in OAuth callback: {ex.Message}\n{ex.StackTrace}\n");
                    Console.WriteLine($"OAuth callback error: {ex.Message}");
                }
                
                // Don't exit - launch app to process the saved token
                File.AppendAllText(debugLog, $"  Launching app after OAuth callback\n");
                LaunchApp();
                return;
            }
            
            File.AppendAllText(debugLog, $"  No URL args, launching app normally\n");
            // Continue to normal app launch
            LaunchApp();
        }

        private static void HandleSetupToken(string setupUrl)
        {
            try
            {
                // Extract token from URL: allumi://setup?token=tok_xyz123
                var uri = new Uri(setupUrl);
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var token = queryParams["token"];
                
                if (!string.IsNullOrEmpty(token))
                {
                    // Save token file for Config.Load() to find
                    var exeDir = AppContext.BaseDirectory;
                    var tokenPath = Path.Combine(exeDir, ".exchange-token");
                    File.WriteAllText(tokenPath, token);
                    
                    Console.WriteLine($"Setup token saved to: {tokenPath}");
                    
                    // Launch the app normally (will pick up the token)
                    // NOTE: Don't show MessageBox here - it creates a window before app initialization!
                    LaunchApp();
                }
                else
                {
                    Console.WriteLine("No token found in setup URL");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling setup token: {ex.Message}");
            }
        }

        private static void LaunchApp()
        {
            try
            {
                // Must set DPI mode BEFORE ApplicationConfiguration.Initialize()
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                
                // Check for updates from GitHub releases (silent auto-update in background)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var githubSource = new Squirrel.Sources.GithubSource("https://github.com/fini-commits/Allumi.WindowsSensor", null, false);
                        using var updateManager = new Squirrel.UpdateManager(githubSource);
                        
                        var updateInfo = await updateManager.CheckForUpdate();
                        
                        if (updateInfo?.ReleasesToApply?.Count > 0)
                        {
                            var newVersion = updateInfo.FutureReleaseEntry.Version.ToString();
                            Debug.WriteLine($"Update available: {newVersion}. Downloading...");
                            
                            // Download and apply update silently
                            await updateManager.DownloadReleases(updateInfo.ReleasesToApply);
                            await updateManager.ApplyReleases(updateInfo);
                            
                            Debug.WriteLine($"Update to {newVersion} installed. Will take effect on next app restart.");
                        }
                        else
                        {
                            Debug.WriteLine("No updates available. Running latest version.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Auto-update check failed: {ex.Message}");
                        Console.WriteLine($"[AUTO-UPDATE ERROR] {ex.Message}");
                    }
                });
                
                ApplicationConfiguration.Initialize();
                Application.Run(new TrayApp());
            }
            catch (Exception ex)
            {
                var errorLog = Path.Combine(AppContext.BaseDirectory, "logs", "crash.log");
                var logDir = Path.GetDirectoryName(errorLog);
                if (!string.IsNullOrEmpty(logDir))
                    Directory.CreateDirectory(logDir);
                File.AppendAllText(errorLog, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] CRASH in LaunchApp:\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n");
                MessageBox.Show($"Fatal error: {ex.Message}\n\nCheck logs\\crash.log for details.", "Allumi Sensor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        private static void AddToStartup()
        {
            try
            {
                string appName = "AllumiWindowsSensor";
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                
                if (string.IsNullOrEmpty(exePath))
                    return;

                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    var existingValue = key.GetValue(appName) as string;
                    // Only add if not already there or path changed
                    if (existingValue != exePath)
                    {
                        key.SetValue(appName, exePath);
                        Console.WriteLine($"Added to Windows startup: {exePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to add to startup: {ex.Message}");
            }
            
            // Check for updates with notification
            _ = Task.Run(async () =>
            {
                await UpdateHelper.CheckAndApplyUpdatesAsync(
                    "https://github.com/fini-commits/Allumi.WindowsSensor",
                    msg => Debug.WriteLine($"[Update] {msg}"),
                    onUpdateAvailable: (version) =>
                    {
                        var result = MessageBox.Show(
                            $"A new version {version} is available!\n\nCurrent version: {AppVersion}\n\nWould you like to update now?",
                            "Update Available - Allumi Sensor",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information);
                        return result == DialogResult.Yes;
                    });
            });
            
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApp());
        }
    }

    // ------------------ TRAY APP ------------------
    public sealed class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly ActivityTracker? _tracker;
        private readonly AppConfig _cfg = null!;
        private readonly string _cfgPath = null!;

        public TrayApp()
        {
            try
            {
                var configResult = Config.Load();
                _cfg = configResult.cfg;
                _cfgPath = configResult.sourcePath;

                // DPI mode is now set in LaunchApp() before ApplicationConfiguration.Initialize()
                // Don't set it here

                // Load icon with fallback to system icon if logo.ico is missing or corrupt
                Icon trayIcon;
                try
                {
                    var logoPath = Path.Combine(AppContext.BaseDirectory, "logo.ico");
                    if (File.Exists(logoPath) && new FileInfo(logoPath).Length > 0)
                    {
                        trayIcon = new Icon(logoPath);
                        Console.WriteLine($"Loaded logo.ico successfully");
                    }
                    else
                    {
                        // Fallback to system application icon
                        trayIcon = SystemIcons.Application;
                        Console.WriteLine($"Using SystemIcons.Application (logo.ico not found or empty)");
                    }
                }
                catch (Exception ex)
                {
                    // If anything fails, use system icon
                    trayIcon = SystemIcons.Application;
                    Console.WriteLine($"Icon load error: {ex.Message}, using SystemIcons.Application");
                }

            _tray = new NotifyIcon
            {
                Icon = trayIcon,
                Visible = true,
                ContextMenuStrip = BuildMenu(),
                Text = $"Allumi Sensor v{Program.AppVersion} • starting…"
            };

            // Check if configuration is valid
            bool hasConfig = !string.IsNullOrWhiteSpace(_cfg?.deviceId)
                          && !string.IsNullOrWhiteSpace(_cfg?.apiKey)
                          && !string.IsNullOrWhiteSpace(_cfg?.syncUrl);

            if (!hasConfig)
            {
                // Automatically trigger authentication on first run
                _tray.Text = "Allumi Sensor • Authenticating...";
                
                // Start authentication without blocking (stays on UI thread)
                _ = AuthenticateAsync();
                
                return; // Don't start tracking yet
            }

            // Build sync client from config (hasConfig check above ensures _cfg is not null)
            var sync = new SyncClient(_cfg!.apiKey ?? "", _cfg.deviceId, _cfg.deviceName, _cfg.syncUrl);

            // Start tracker
            _tracker = new ActivityTracker(sync, _cfg.deviceId, _cfg.deviceName);
            _tracker.OnTrayText += t => _tray.Text = $"Allumi Sensor v{Program.AppVersion} • {t}";
            _tracker.Start();

            // Start diagnostics reporting (every 5 minutes)
            StartDiagnosticsReporting();

                _tray.BalloonTipTitle = "Allumi Sensor";
                _tray.BalloonTipText = "Tracking active window.";
                _tray.ShowBalloonTip(3000);
            }
            catch (Exception ex)
            {
                var errorLog = Path.Combine(AppContext.BaseDirectory, "logs", "crash.log");
                var logDir = Path.GetDirectoryName(errorLog);
                if (!string.IsNullOrEmpty(logDir))
                    Directory.CreateDirectory(logDir);
                File.AppendAllText(errorLog, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] CRASH in TrayApp constructor:\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n");
                Console.WriteLine($"[TRAYAPP ERROR] {ex.Message}");
                throw;
            }
        }        private void StartDiagnosticsReporting()
        {
            _ = Task.Run(async () =>
            {
                var diagnostics = new Diagnostics.DiagnosticsReporter(_cfg.syncUrl.Replace("/sync-device-activity", ""), _cfg.deviceId, _cfg.apiKey);
                
                while (true)
                {
                    try
                    {
                        await diagnostics.ReportHealthCheckAsync();
                        await Task.Delay(TimeSpan.FromMinutes(5)); // Report every 5 minutes
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Diagnostics reporting error: {ex.Message}");
                        await Task.Delay(TimeSpan.FromMinutes(5)); // Retry after 5 minutes
                    }
                }
            });
        }

        private async Task AuthenticateAsync()
        {
            try
            {
                _tray.Text = "Allumi Sensor • Authenticating...";
                
                // Open browser to Allumi for device authentication
                string allumiBaseUrl = "https://allumi.ai";
                
                var token = await OAuthHandler.AuthenticateAsync(allumiBaseUrl);
                
                if (!string.IsNullOrEmpty(token))
                {
                    // Save token to file so Config.Load() can pick it up
                    var exeDir = AppContext.BaseDirectory;
                    var tokenPath = Path.Combine(exeDir, ".exchange-token");
                    File.WriteAllText(tokenPath, token);
                    
                    // Restart app - it will pick up the token and exchange it
                    MessageBox.Show(
                        "Authentication successful! The app will now restart.",
                        "Allumi Sensor",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    
                    // Release mutex before restart to allow new instance
                    Program._singleInstanceMutex?.ReleaseMutex();
                    Program._singleInstanceMutex?.Dispose();
                    
                    Application.Restart();
                    Application.Exit();
                }
                else
                {
                    _tray.Text = "Allumi Sensor • Authentication Failed";
                    MessageBox.Show(
                        "Authentication failed or timed out. Please try again.",
                        "Allumi Sensor",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Authentication error: {ex.Message}",
                    "Allumi Sensor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            
            // Apply dark mode styling based on system theme
            menu.Renderer = new ToolStripProfessionalRenderer(new DarkModeColorTable());
            menu.BackColor = Color.FromArgb(37, 37, 38); // Dark background
            menu.ForeColor = Color.White; // White text

            // Main actions group
            var openLog = new ToolStripMenuItem("Open Log Folder", null, (_, __) =>
            {
                var path = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(path);
                try { Process.Start("explorer.exe", path); } catch { }
            });
            openLog.ForeColor = Color.White;
            menu.Items.Add(openLog);

            var liveTail = new ToolStripMenuItem("Live Tail (PowerShell)", null, (_, __) =>
            {
                var log = Path.Combine(AppContext.BaseDirectory, "logs", "sensor.log");
                Directory.CreateDirectory(Path.GetDirectoryName(log) ?? "");
                if (!File.Exists(log))
                {
                    File.WriteAllText(log, "# Sensor log - waiting for activities...\n");
                }
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -Command Get-Content -Path '{log}' -Wait",
                    UseShellExecute = true
                };
                try { Process.Start(psi); } catch { }
            });
            liveTail.ForeColor = Color.White;
            menu.Items.Add(liveTail);

            menu.Items.Add(new ToolStripSeparator());

            // Info group
            var version = new ToolStripMenuItem($"Version {Program.AppVersion}") { Enabled = false };
            version.ForeColor = Color.LightGray; // Slightly dimmed for disabled items
            menu.Items.Add(version);
            
            var showConfig = new ToolStripMenuItem("Show config path", null, (_, __) =>
                MessageBox.Show(_cfgPath, "Allumi Sensor", MessageBoxButtons.OK, MessageBoxIcon.Information));
            showConfig.ForeColor = Color.White;
            menu.Items.Add(showConfig);
            
            var deviceInfo = new ToolStripMenuItem("Device Info", null, (_, __) =>
                MessageBox.Show($"Device ID: {_cfg.deviceId}\nDevice Name: {_cfg.deviceName}", "Device Info", MessageBoxButtons.OK, MessageBoxIcon.Information));
            deviceInfo.ForeColor = Color.White;
            menu.Items.Add(deviceInfo);

            menu.Items.Add(new ToolStripSeparator());

            // Update group
            var checkUpdates = new ToolStripMenuItem("Check for Updates", null, async (_, __) => await CheckForUpdatesAsync());
            checkUpdates.ForeColor = Color.White;
            menu.Items.Add(checkUpdates);

            menu.Items.Add(new ToolStripSeparator());

            // About & Support group
            var about = new ToolStripMenuItem("About", null, (_, __) =>
                MessageBox.Show($"Allumi Sensor\nVersion: {Program.AppVersion}\n© 2025 Allumi.ai\nPrivacy: https://allumi.ai/privacy-policy", "About Allumi Sensor", MessageBoxButtons.OK, MessageBoxIcon.Information));
            about.ForeColor = Color.White;
            menu.Items.Add(about);
            
            var support = new ToolStripMenuItem("Report a Bug / Contact Support", null, (_, __) =>
            {
                // Include device ID and user ID in support URL for auto-identification
                var supportUrl = $"https://allumi.ai/support?deviceId={Uri.EscapeDataString(_cfg.deviceId ?? "")}&userId={Uri.EscapeDataString(_cfg.userId ?? "")}";
                Process.Start(new ProcessStartInfo { FileName = supportUrl, UseShellExecute = true });
            });
            support.ForeColor = Color.White;
            menu.Items.Add(support);

            menu.Items.Add(new ToolStripSeparator());

            // Quit
            var quit = new ToolStripMenuItem("Quit", null, (_, __) => ExitThread());
            quit.ForeColor = Color.White;
            menu.Items.Add(quit);
            
            return menu;
        }

        private async Task CheckForUpdatesAsync()
        {
            _tray.Text = $"Allumi Sensor v{Program.AppVersion} • Checking for updates...";
            
            await UpdateHelper.CheckAndApplyUpdatesAsync(
                "https://github.com/fini-commits/Allumi.WindowsSensor",
                msg => Debug.WriteLine($"[Update] {msg}"),
                onUpdateAvailable: (version) =>
                {
                    var result = MessageBox.Show(
                        $"A new version {version} is available!\n\nCurrent version: {Program.AppVersion}\n\nWould you like to update now?",
                        "Update Available - Allumi Sensor",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);
                    return result == DialogResult.Yes;
                },
                showNoUpdateMessage: true);
            
            // Restore tray text
            if (_tracker != null)
            {
                _tray.Text = $"Allumi Sensor v{Program.AppVersion} • Tracking";
            }
        }

        protected override void ExitThreadCore()
        {
            _tracker?.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            base.ExitThreadCore();
        }
    }

    // ------------------ ACTIVITY TRACKER ------------------
    public sealed class ActivityTracker : IDisposable
    {
        private readonly System.Timers.Timer _poll = new(250); // ~4x/sec
        // No longer need sync timer - we sync immediately on each activity
        private readonly TimeSpan _idleThreshold = TimeSpan.FromSeconds(60);
        private readonly SyncClient _sync;
        private readonly string _deviceId;
        private readonly string _deviceName;

        private string _curProc = "";
        private string _curTitle = "";
        private bool _isIdle = false;
        private DateTime _curStart = DateTime.UtcNow;

        // Deduplication tracking
        private string _lastSentKey = "";
        private DateTime _lastSentTime = DateTime.MinValue;
        private readonly TimeSpan _minTimeBetweenDuplicates = TimeSpan.FromSeconds(2);

        private readonly string _logPath;

        public event Action<string>? OnTrayText;

        public ActivityTracker(SyncClient sync, string deviceId, string deviceName)
        {
            _sync = sync;
            _deviceId = deviceId;
            _deviceName = deviceName;
            
            // Use EXE directory for logs (works with Squirrel)
            var exeDir = AppContext.BaseDirectory;
            var logDir = Path.Combine(exeDir, "logs");
            Directory.CreateDirectory(logDir);
            _logPath = Path.Combine(logDir, "sensor.log");

            _poll.Elapsed += (_, __) => Tick();
            // No sync timer - we sync immediately on each activity
        }

        public void Start()
        {
            _curStart = DateTime.UtcNow;
            _poll.Start();
            // No sync timer to start
        }

        public void Stop()
        {
            _poll.Stop();
            // No sync timer to stop
            CloseCurrentSession(force: true);
            // Final flush on exit
            _ = _sync.FlushActivitiesAsync();
        }

        // Removed SyncActivitiesAsync - now syncing immediately on each activity

        private void Tick()
        {
            var now = DateTime.UtcNow;

            // Idle calc
            var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            bool ok = GetLastInputInfo(ref lii);
            var idleMs = ok ? Environment.TickCount - (int)lii.dwTime : 0;
            var isIdle = idleMs >= _idleThreshold.TotalMilliseconds;

            // Foreground window
            var hWnd = GetForegroundWindow();
            string process = "";
            string title = "";
            if (hWnd != IntPtr.Zero)
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                try { process = Process.GetProcessById((int)pid).ProcessName; } catch { }
                title = GetWindowTitle(hWnd);
            }

            bool changed = isIdle != _isIdle || process != _curProc || title != _curTitle;
            if (!changed) return;

            CloseCurrentSession();

            _curProc = process;
            _curTitle = title;
            _isIdle = isIdle;
            _curStart = now;

            var label = string.IsNullOrWhiteSpace(process) ? "no window" : process;
            var status = isIdle ? "idle" : "active";
            OnTrayText?.Invoke($"{label} • {status}");
        }

        private void CloseCurrentSession(bool force = false)
        {
            if (string.IsNullOrEmpty(_curProc) && !force) return;

            var now = DateTime.UtcNow;
            var dur = (int)Math.Max(0, (now - _curStart).TotalSeconds);

            // Skip very short sessions (< 1 second)
            if (dur < 1 && !force) return;

            // Smart categorization BEFORE sending
            var categoryResult = ActivityCategorizer.Categorize(_curProc, _curTitle);
            
            // DEDUPLICATION: Create unique key for this activity
            // Round start time to nearest second to catch rapid duplicates
            var roundedStart = new DateTime(
                _curStart.Year, _curStart.Month, _curStart.Day,
                _curStart.Hour, _curStart.Minute, _curStart.Second, DateTimeKind.Utc);
            var activityKey = $"{_curProc}|{_curTitle}|{roundedStart:O}";
            
            // Check if this is a duplicate within the debounce window
            if (activityKey == _lastSentKey && (now - _lastSentTime) < _minTimeBetweenDuplicates)
            {
                // This is a duplicate - skip sending
                try { File.AppendAllText(_logPath, $"{now:O}\tDUPLICATE SKIPPED: {_curProc} - {_curTitle}{Environment.NewLine}"); } catch { }
                return;
            }
            
            // Update deduplication tracking
            _lastSentKey = activityKey;
            _lastSentTime = now;
            
            // local debug log with category
            var status = _isIdle ? "idle" : "active";
            var line = $"{now:O}\tproc={_curProc}\ttitle={_curTitle}\tcategory={categoryResult.Category}\tconfidence={categoryResult.Confidence}%\tstatus={status}\tdur={dur}s";
            try { File.AppendAllText(_logPath, line + Environment.NewLine); } catch { }

            // Create activity event with smart categorization
            var ev = new Models.ActivityEvent
            {
                appName = _curProc,
                windowTitle = _curTitle,
                startTime = roundedStart.ToString("O"),  // Use rounded time for consistency
                endTime = now.ToUniversalTime().ToString("O"),
                durationSeconds = dur,
                category = categoryResult.Category,  // Smart category from our analyzer
                isIdle = _isIdle
            };
            
            // Send IMMEDIATELY to database (not queued)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _sync.SendActivityImmediatelyAsync(ev);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Activity Sync] Error: {ex.Message}");
                }
            });
        }

        public void Dispose()
        {
            Stop();
            _poll.Dispose();
        }

        // ------------------ Win32 P/Invoke ------------------
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private static string GetWindowTitle(IntPtr hWnd)
        {
            int length = GetWindowTextLength(hWnd);
            if (length <= 0) return string.Empty;
            var sb = new StringBuilder(length + 2);
            _ = GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString();
            
            // Normalize fancy Unicode characters to plain text
            return NormalizeUnicodeText(title);
        }

        /// <summary>
        /// Converts fancy Unicode characters (bold, italic, etc.) to plain ASCII equivalents
        /// </summary>
        private static string NormalizeUnicodeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // Use .NET's Unicode normalization to decompose characters
            var normalized = text.Normalize(System.Text.NormalizationForm.FormKD);
            
            var sb = new StringBuilder(normalized.Length);
            
            foreach (char c in normalized)
            {
                // Skip combining characters and control characters
                if (char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark &&
                    char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Control)
                {
                    // Only keep ASCII-compatible characters
                    if (c <= 127)
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        // For characters outside ASCII, try to find ASCII equivalent
                        // Mathematical Bold/Italic/etc (0x1D400-0x1D7FF range)
                        if (c >= 0x1D400 && c <= 0x1D7FF)
                        {
                            // Map back to normal ASCII
                            int offset = c - 0x1D400;
                            int baseOffset = offset % 52; // 26 uppercase + 26 lowercase
                            if (baseOffset < 26) sb.Append((char)('A' + baseOffset));
                            else if (baseOffset < 52) sb.Append((char)('a' + (baseOffset - 26)));
                            else sb.Append(c); // Keep as-is if not a letter
                        }
                        // Keep other non-ASCII characters (e.g., proper names)
                        else
                        {
                            sb.Append(c);
                        }
                    }
                }
            }
            
            return sb.ToString();
        }
    }
}
