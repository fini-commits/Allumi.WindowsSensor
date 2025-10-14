using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Allumi.WindowsSensor.Models;   // <- you added this
using Allumi.WindowsSensor.Sync;     // <- and this
using Allumi.WindowsSensor.Update;

namespace Allumi.WindowsSensor
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            UpdateHelper.HandleSquirrelEvents();
            _ = UpdateHelper.CheckAndApplyUpdatesAsync("https://your-host/updates");
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApp());
        }
    }

    // ------------------ TRAY APP ------------------
    public sealed class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly ActivityTracker _tracker;
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
                Text = "Allumi Sensor • starting…"
            };

            // Build sync client from config.json
            var sync = new SyncClient(_cfg.apiKey, _cfg.deviceId, _cfg.syncUrl);

            // Start tracker
            _tracker = new ActivityTracker(sync);
            _tracker.OnTrayText += t => _tray.Text = $"Allumi Sensor • {t}";
            _tracker.Start();

            // First-run tip
            bool ok = !string.IsNullOrWhiteSpace(_cfg?.deviceId)
                   && !string.IsNullOrWhiteSpace(_cfg?.apiKey)
                   && !string.IsNullOrWhiteSpace(_cfg?.syncUrl);

            _tray.BalloonTipTitle = "Allumi Sensor";
            _tray.BalloonTipText  = ok
                ? "Tracking active window (instant sync)."
                : "Config not found or incomplete. Right-click → Show config path.";
            _tray.ShowBalloonTip(3000);
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
            menu.Items.Add("Show config path", null, (_, __) =>
                MessageBox.Show(_cfgPath, "Allumi Sensor", MessageBoxButtons.OK, MessageBoxIcon.Information));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit", null, (_, __) => ExitThread());
            return menu;
        }

        protected override void ExitThreadCore()
        {
            _tracker.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            base.ExitThreadCore();
        }
    }

    // ------------------ ACTIVITY TRACKER ------------------
    public sealed class ActivityTracker : IDisposable
    {
        private readonly System.Timers.Timer _poll = new(250); // ~4x/sec
        private readonly TimeSpan _idleThreshold = TimeSpan.FromSeconds(60);
        private readonly SyncClient _sync;

        private string _curProc = "";
        private string _curTitle = "";
        private string _curStatus = "active";
        private DateTime _curStart = DateTime.UtcNow;

        private readonly string _logPath;

        public event Action<string>? OnTrayText;

        public ActivityTracker(SyncClient sync)
        {
            _sync = sync;
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(roaming, "Allumi");
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "sensor.log");

            _poll.Elapsed += (_, __) => Tick();
        }

        public void Start()
        {
            _curStart = DateTime.UtcNow;
            _poll.Start();
        }

        public void Stop()
        {
            _poll.Stop();
            CloseCurrentSession(force: true);
        }

        private void Tick()
        {
            var now = DateTime.UtcNow;

            // Idle calc
            var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            bool ok = GetLastInputInfo(ref lii);
            var idleMs = ok ? Environment.TickCount - (int)lii.dwTime : 0;
            var status = idleMs >= _idleThreshold.TotalMilliseconds ? "idle" : "active";

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

            bool changed = status != _curStatus || process != _curProc || title != _curTitle;
            if (!changed) return;

            CloseCurrentSession();

            _curProc = process;
            _curTitle = title;
            _curStatus = status;
            _curStart = now;

            var label = string.IsNullOrWhiteSpace(process) ? "no window" : process;
            OnTrayText?.Invoke($"{label} • {status}");
        }

        private void CloseCurrentSession(bool force = false)
        {
            if (string.IsNullOrEmpty(_curProc) && !force) return;

            var now = DateTime.UtcNow;
            var dur = (int)Math.Max(0, (now - _curStart).TotalSeconds);

            // local debug log
            var line = $"{now:O}\tproc={_curProc}\ttitle={_curTitle}\tstatus={_curStatus}\tdur={dur}s";
            try { File.AppendAllText(_logPath, line + Environment.NewLine); } catch { }

            // send instantly (fire-and-forget)
            var ev = new Models.ActivityEvent
            {
                app_name = _curProc,
                window_title = _curTitle,
                status = _curStatus,
                started_at = _curStart,
                ended_at = now,
                duration_seconds = dur
            };
            _ = _sync.SendEventAsync(ev);
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
