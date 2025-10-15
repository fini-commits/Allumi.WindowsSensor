using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace Allumi.WindowsSensor.Auth
{
    /// <summary>
    /// Handles OAuth authentication flow via browser and allumi:// protocol
    /// </summary>
    public static class OAuthHandler
    {
        private const string PROTOCOL_SCHEME = "allumi";
        private static HttpListener? _listener;
        private static TaskCompletionSource<string>? _authResultTcs;

        /// <summary>
        /// Registers the allumi:// protocol handler in Windows Registry
        /// </summary>
        public static bool RegisterProtocolHandler()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                
                if (string.IsNullOrEmpty(exePath))
                    return false;

                using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{PROTOCOL_SCHEME}");
                key.SetValue("", $"URL:{PROTOCOL_SCHEME} Protocol");
                key.SetValue("URL Protocol", "");

                using var commandKey = key.CreateSubKey(@"shell\open\command");
                commandKey.SetValue("", $"\"{exePath}\" \"%1\"");

                Console.WriteLine($"Registered {PROTOCOL_SCHEME}:// protocol handler");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to register protocol handler: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initiates OAuth flow by opening browser to Vetra auth page
        /// </summary>
        /// <param name="vetraBaseUrl">Base URL of Vetra web app (e.g., https://vetra.app)</param>
        /// <param name="timeoutSeconds">Timeout in seconds to wait for authentication</param>
        /// <returns>Authentication token or null if failed/timeout</returns>
        public static async Task<string?> AuthenticateAsync(string vetraBaseUrl, int timeoutSeconds = 300)
        {
            try
            {
                // Generate a unique device ID for this session
                string deviceId = $"windows-{Guid.NewGuid():N}";
                
                // Build auth URL that will redirect back to allumi://
                string authUrl = $"{vetraBaseUrl}/auth/device-login?device_id={deviceId}&callback={Uri.EscapeDataString($"{PROTOCOL_SCHEME}://auth")}";
                
                Console.WriteLine($"Opening authentication URL: {authUrl}");
                
                // Start listening for the OAuth callback
                _authResultTcs = new TaskCompletionSource<string>();
                
                // Start local HTTP listener as fallback (allumi:// might not work)
                StartLocalListener();
                
                // Open browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });
                
                // Wait for auth result or timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                var completedTask = await Task.WhenAny(_authResultTcs.Task, timeoutTask);
                
                StopLocalListener();
                
                if (completedTask == timeoutTask)
                {
                    Console.WriteLine("Authentication timed out");
                    return null;
                }
                
                return await _authResultTcs.Task;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Authentication failed: {ex.Message}");
                StopLocalListener();
                return null;
            }
        }

        /// <summary>
        /// Handles allumi:// protocol callback (called when app is launched with allumi:// URL)
        /// </summary>
        /// <param name="protocolUrl">The full allumi:// URL received</param>
        public static void HandleProtocolCallback(string protocolUrl)
        {
            try
            {
                var uri = new Uri(protocolUrl);
                var query = HttpUtility.ParseQueryString(uri.Query);
                
                string? token = query["token"];
                string? configJson = query["config"];
                
                if (!string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("Received auth token via protocol callback");
                    _authResultTcs?.TrySetResult(token);
                }
                else if (!string.IsNullOrEmpty(configJson))
                {
                    Console.WriteLine("Received config via protocol callback");
                    _authResultTcs?.TrySetResult(configJson);
                }
                else
                {
                    Console.WriteLine("Protocol callback missing required parameters");
                    _authResultTcs?.TrySetResult("");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to handle protocol callback: {ex.Message}");
                _authResultTcs?.TrySetResult("");
            }
        }

        private static void StartLocalListener()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:42813/");
                _listener.Start();
                
                _ = Task.Run(async () =>
                {
                    while (_listener.IsListening)
                    {
                        try
                        {
                            var context = await _listener.GetContextAsync();
                            var query = context.Request.QueryString;
                            
                            string? token = query["token"];
                            string? config = query["config"];
                            
                            // Send response to browser
                            string responseHtml = @"
                                <html><body style='font-family: Arial; text-align: center; padding: 50px;'>
                                    <h2>✅ Authentication Successful!</h2>
                                    <p>You can close this window and return to the Allumi app.</p>
                                    <script>window.close();</script>
                                </body></html>";
                            
                            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
                            context.Response.ContentLength64 = buffer.Length;
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                            context.Response.OutputStream.Close();
                            
                            if (!string.IsNullOrEmpty(token))
                            {
                                _authResultTcs?.TrySetResult(token);
                            }
                            else if (!string.IsNullOrEmpty(config))
                            {
                                _authResultTcs?.TrySetResult(config);
                            }
                            
                            break;
                        }
                        catch { }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start local listener: {ex.Message}");
            }
        }

        private static void StopLocalListener()
        {
            try
            {
                _listener?.Stop();
                _listener?.Close();
                _listener = null;
            }
            catch { }
        }
    }
}
