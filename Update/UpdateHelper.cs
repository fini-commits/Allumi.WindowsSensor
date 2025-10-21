using System;
using System.Threading.Tasks;
using Squirrel;

namespace Allumi.WindowsSensor.Update
{
    public static class UpdateHelper
    {
        // feedUrl will point to your hosted RELEASES feed
        public static async Task CheckAndApplyUpdatesAsync(
            string feedUrl, 
            Action<string>? log = null,
            Func<string, bool>? onUpdateAvailable = null,
            bool showNoUpdateMessage = false)
        {
            try
            {
                using var mgr = new UpdateManager(feedUrl);
                log?.Invoke("Checking for updates…");
                
                var updateInfo = await mgr.CheckForUpdate();
                if (updateInfo?.ReleasesToApply?.Count > 0)
                {
                    var newVersion = updateInfo.FutureReleaseEntry.Version.ToString();
                    log?.Invoke($"Update available: {newVersion}");
                    
                    // Ask user if they want to update
                    bool shouldUpdate = onUpdateAvailable?.Invoke(newVersion) ?? true;
                    
                    if (shouldUpdate)
                    {
                        log?.Invoke("Downloading and applying update…");
                        var result = await mgr.UpdateApp();
                        if (result is not null)
                        {
                            log?.Invoke($"Updated to {result.Version}. Please restart the app.");
                            System.Windows.Forms.MessageBox.Show(
                                $"Update to version {result.Version} has been installed.\n\nThe app will restart now.",
                                "Update Complete",
                                System.Windows.Forms.MessageBoxButtons.OK,
                                System.Windows.Forms.MessageBoxIcon.Information);
                            Squirrel.UpdateManager.RestartApp();
                        }
                    }
                    else
                    {
                        log?.Invoke("Update postponed by user.");
                        System.Windows.Forms.MessageBox.Show(
                            $"Update to version {newVersion} is available.\n\nIt will be installed on the next app launch.",
                            "Update Available",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            System.Windows.Forms.MessageBoxIcon.Information);
                    }
                }
                else
                {
                    log?.Invoke("No updates available.");
                    if (showNoUpdateMessage)
                    {
                        var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
                        System.Windows.Forms.MessageBox.Show(
                            $"You are running the latest version ({currentVersion}).",
                            "No Updates Available",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            System.Windows.Forms.MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Update check failed: {ex.Message}");
                if (showNoUpdateMessage)
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"Failed to check for updates:\n\n{ex.Message}",
                        "Update Check Failed",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                }
            }
        }

        public static void HandleSquirrelEvents()
        {
            try
            {
                // Don't auto-launch on install - let the URL scheme launch with token
                SquirrelAwareApp.HandleEvents();
            }
            catch (Exception) { }
        }
    }
}
