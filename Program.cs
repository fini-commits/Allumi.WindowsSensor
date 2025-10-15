using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using System.Reflection;
using Allumi.WindowsSensor.Models;
using Allumi.WindowsSensor.Sync;
using Allumi.WindowsSensor.Update;
using Allumi.WindowsSensor.Auth;
using Allumi.WindowsSensor.Categorization;

namespace Allumi.WindowsSensor
{
    internal static class Program
    {
        public static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.21";
        
        [STAThread]
        static void Main(string[] args)
        {
            // Handle Squirrel installation events
            UpdateHelper.HandleSquirrelEvents();
            
            // Register OAuth protocol handler (allumi://)
            OAuthHandler.RegisterProtocolHandler();
            
            // Check if launched via allumi:// protocol
            if (args.Length > 0 && args[0].StartsWith("allumi://", StringComparison.OrdinalIgnoreCase))
            {
                OAuthHandler.HandleProtocolCallback(args[0]);
                return; // Exit after handling callback
            }
            
            // Check for updates with notification
            _ = Task.Run(async () =>
            {
                await UpdateHelper.CheckAndApplyUpdatesAsync(
                    "https://your-host/updates",
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
            var configResult = Config.Load();
            _cfg = configResult.cfg;
            _cfgPath = configResult.sourcePath;

            _tray = new NotifyIcon
            {
                Icon = SystemIcons.Information,
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
                // Show authentication prompt
                _tray.BalloonTipTitle = "Allumi Sensor - Setup Required";
                _tray.BalloonTipText = "Click to authenticate with your Vetra account";
                _tray.BalloonTipClicked += async (_, __) => await AuthenticateAsync();
                _tray.ShowBalloonTip(5000);
                _tray.Text = "Allumi Sensor • Not Authenticated";
                return; // Don't start tracking yet
            }

            // Build sync client from config
            var sync = new SyncClient(_cfg.apiKey ?? "", _cfg.deviceId, _cfg.deviceName, _cfg.syncUrl);

            // Start tracker
            _tracker = new ActivityTracker(sync, _cfg.deviceId, _cfg.deviceName);
            _tracker.OnTrayText += t => _tray.Text = $"Allumi Sensor v{Program.AppVersion} • {t}";
            _tracker.Start();

            _tray.BalloonTipTitle = "Allumi Sensor";
            _tray.BalloonTipText = "Tracking active window.";
            _tray.ShowBalloonTip(3000);
        }

        private async Task AuthenticateAsync()
        {
            try
            {
                _tray.Text = "Allumi Sensor • Authenticating...";
                
                // Use supabaseUrl from config if available, otherwise use default
                string vetraBaseUrl = _cfg?.supabaseUrl ?? "https://lstannxhfhunacgkvtmm.supabase.co";
                
                var result = await OAuthHandler.AuthenticateAsync(vetraBaseUrl);
                
                if (!string.IsNullOrEmpty(result))
                {
                    // Authentication successful - reload config and restart
                    MessageBox.Show(
                        "Authentication successful! The app will now restart.",
                        "Allumi Sensor",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    
                    Application.Restart();
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

            // Open Log Folder
            menu.Items.Add("Open Log Folder", null, (_, __) =>
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Allumi");
                try { Process.Start("explorer.exe", path); } catch { }
            });

            // Live Tail (PowerShell)
            menu.Items.Add("Live Tail (PowerShell)", null, (_, __) =>
            {
                var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Allumi", "sensor.log");
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -Command Get-Content -Path '{log}' -Wait",
                    UseShellExecute = true
                };
                try { Process.Start(psi); } catch { }
            });

            menu.Items.Add(new ToolStripSeparator());
            
            // Version info
            var versionItem = new ToolStripMenuItem($"Version {Program.AppVersion}")
            {
                Enabled = false
            };
            menu.Items.Add(versionItem);
            
            // Check for Updates
            menu.Items.Add("Check for Updates", null, async (_, __) => await CheckForUpdatesAsync());
            
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Show config path", null, (_, __) =>
                MessageBox.Show(_cfgPath, "Allumi Sensor", MessageBoxButtons.OK, MessageBoxIcon.Information));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit", null, (_, __) => ExitThread());
            return menu;
        }

        private async Task CheckForUpdatesAsync()
        {
            _tray.Text = $"Allumi Sensor v{Program.AppVersion} • Checking for updates...";
            
            await UpdateHelper.CheckAndApplyUpdatesAsync(
                "https://your-host/updates",
                msg => Debug.WriteLine($"[Update] {msg}"),
                onUpdateAvailable: (version) =>
                {
                    var result = MessageBox.Show(
                        $"A new version {version} is available!\n\nCurrent version: {Program.AppVersion}\n\nWould you like to update now?",
                        "Update Available - Allumi Sensor",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);
                    return result == DialogResult.Yes;
                });
            
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

        private readonly string _logPath;

        public event Action<string>? OnTrayText;

        public ActivityTracker(SyncClient sync, string deviceId, string deviceName)
        {
            _sync = sync;
            _deviceId = deviceId;
            _deviceName = deviceName;
            
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(roaming, "Allumi");
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "sensor.log");

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
            
            // local debug log with category
            var status = _isIdle ? "idle" : "active";
            var line = $"{now:O}\tproc={_curProc}\ttitle={_curTitle}\tcategory={categoryResult.Category}\tconfidence={categoryResult.Confidence}%\tstatus={status}\tdur={dur}s";
            try { File.AppendAllText(_logPath, line + Environment.NewLine); } catch { }

            // Create activity event with smart categorization
            var ev = new Models.ActivityEvent
            {
                appName = _curProc,
                windowTitle = _curTitle,
                startTime = _curStart.ToUniversalTime().ToString("O"),  // ISO 8601
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
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
            return sb.ToString();
        }
    }
}
