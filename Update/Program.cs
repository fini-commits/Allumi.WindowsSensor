using System;
using System.Threading.Tasks;
using Squirrel;

namespace Allumi.WindowsSensor.Update
{
    public static class UpdateHelper
    {
        // feedUrl will point to your hosted Squirrel 'RELEASES' later (we'll set it in a next step)
        public static async Task CheckAndApplyUpdatesAsync(string feedUrl, Action<string>? log = null)
        {
            try
            {
                using var mgr = new UpdateManager(feedUrl);
                log?.Invoke("Checking for updates…");
                var result = await mgr.UpdateApp();
                if (result is not null)
                {
                    log?.Invoke($"Updated to {result.Version}.");
                }
                else
                {
                    log?.Invoke("No updates available.");
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Update check failed: {ex.Message}");
            }
        }

        public static void HandleSquirrelEvents(Action<string>? log = null)
        {
            try
            {
                SquirrelAwareApp.HandleEvents(
                    onInitialInstall: v => { /* e.g., create shortcuts if needed */ },
                    onAppUpdate: v => { /* e.g., refresh shortcuts */ },
                    onAppUninstall: v => { /* cleanup if needed */ },
                    onFirstRun: () => { /* first run after install */ }
                );
            }
            catch (Exception ex)
            {
                log?.Invoke($"Squirrel events error: {ex.Message}");
            }
        }

        public static async Task HandleSquirrelEventsAsync(string[] args, Action<string>? log = null)
        {
            try
            {
                SquirrelAwareApp.HandleEvents(
                    onInitialInstall: v => { /* e.g., create shortcuts if needed */ },
                    onAppUpdate: v => { /* e.g., refresh shortcuts */ },
                    onAppUninstall: v => { /* cleanup if needed */ },
                    onFirstRun: () => { /* first run after install */ }
                );
            }
            catch (Exception ex)
            {
                log?.Invoke($"Squirrel events error: {ex.Message}");
            }
            await Task.CompletedTask;
        }
    }
}
