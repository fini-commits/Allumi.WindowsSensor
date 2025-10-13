using System;
using System.Threading.Tasks;
using Squirrel;

namespace Allumi.WindowsSensor.Update
{
    public static class UpdateHelper
    {
        // feedUrl will point to your hosted RELEASES feed; we’ll set it later
        public static async Task CheckAndApplyUpdatesAsync(string feedUrl, Action<string>? log = null)
        {
            try
            {
                using var mgr = new UpdateManager(feedUrl);
                log?.Invoke("Checking for updates…");
                var result = await mgr.UpdateApp();
                if (result is not null)
                    log?.Invoke($"Updated to {result.Version}.");
                else
                    log?.Invoke("No updates available.");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Update check failed: {ex.Message}");
            }
        }

        public static void HandleSquirrelEvents()
        {
            try
            {
                SquirrelAwareApp.HandleEvents(
                    onInitialInstall: v => { /* first install */ },
                    onAppUpdate: v => { /* after update */ },
                    onAppUninstall: v => { /* cleanup */ },
                    onFirstRun: () => { /* first run */ }
                );
            }
            catch (Exception) { }
        }
    }
}
