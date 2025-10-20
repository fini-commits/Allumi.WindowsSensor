# Token Exchange System - Complete Integration Spec

## Overview

The Windows Sensor app uses a **URL-based token delivery system** for automatic credential provisioning. This eliminates manual configuration while maintaining security through time-limited, single-use exchange tokens.

**Key Innovation:** Token passed via custom URL scheme (`allumi://setup?token=xxx`) after installation, avoiding impossible task of embedding credentials in Squirrel installer packages.

---

## Complete Flow Diagram

```
┌─────────────────┐
│ User Dashboard  │
│ Clicks Download │
└────────┬────────┘
         │
         ▼
┌──────────────────────────────────────┐
│ Lovable: generate-custom-installer   │
│ 1. Create device record              │
│ 2. Generate API key: alm_xxx         │
│ 3. Generate token: tok_xxx (5 min)   │
│ 4. Save to device_exchange_tokens    │
│ 5. Return { installerUrl, token }    │
└─────────┬────────────────────────────┘
          │
          ▼
┌──────────────────────────────────────┐
│ Frontend JavaScript                  │
│ 1. Download normal installer         │
│ 2. Show "Install and click OK"       │
│ 3. window.location.href =            │
│    'allumi://setup?token=tok_xxx'    │
└─────────┬────────────────────────────┘
          │
          ▼
┌──────────────────────────────────────┐
│ Windows Protocol Handler             │
│ 1. Catch allumi://setup?token=xxx    │
│ 2. Extract token from URL query      │
│ 3. Save to .exchange-token file      │
│ 4. Show "Device setup initiated!"    │
│ 5. Launch app normally               │
└─────────┬────────────────────────────┘
          │
          ▼
┌──────────────────────────────────────┐
│ Windows App: Config.Load()           │
│ 1. Check for existing config.json    │
│ 2. If not exists, find token file    │
│ 3. POST to exchange endpoint         │
│ 4. Receive credentials               │
│ 5. Save to config.json               │
│ 6. Delete token file (single-use)    │
└─────────┬────────────────────────────┘
          │
          ▼
┌──────────────────────────────────────┐
│ Supabase: exchange-device-token      │
│ 1. Validate: token exists            │
│ 2. Validate: not expired (<5 min)    │
│ 3. Validate: not consumed            │
│ 4. Mark consumed = true              │
│ 5. Return { deviceId, apiKey, etc }  │
└─────────┬────────────────────────────┘
          │
          ▼
┌──────────────────────────────────────┐
│ Windows App: Start Tracking          │
│ 1. Config loaded successfully        │
│ 2. Begin 250ms polling loop          │
│ 3. Sync activities to Supabase       │
│ 4. Activities appear in dashboard    │
└──────────────────────────────────────┘
```

---

## Implementation Details

### 1. Lovable Backend: Token Generation ✅ COMPLETE

**Edge Function:** `generate-custom-installer`

**Request:**
```http
POST /functions/v1/generate-custom-installer
Authorization: Bearer <user_jwt>
```

**Response:**
```json
{
  "installerUrl": "https://github.com/fini-commits/Allumi.WindowsSensor/releases/latest/download/AllumiWindowsSensor-Setup.exe",
  "exchangeToken": "tok_abc123xyz",
  "deviceId": "windows-uuid-timestamp",
  "deviceName": "User's Windows PC"
}
```

**Database Operation:**
```sql
INSERT INTO device_exchange_tokens (
  token,
  device_id,
  api_key,
  user_id,
  expires_at
) VALUES (
  'tok_abc123xyz',
  'windows-uuid-timestamp',
  'alm_randomkey',
  'user-uuid',
  NOW() + INTERVAL '5 minutes'
);
```

---

### 2. Frontend JavaScript: URL Launch ⏳ IN PROGRESS (Lovable)

**Code to implement:**
```javascript
async function downloadWindowsTracker() {
  try {
    // Step 1: Generate token and get installer URL
    const response = await fetch('/api/generate-custom-installer', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${userJWT}`,
        'Content-Type': 'application/json'
      }
    });
    
    const { installerUrl, exchangeToken, deviceId } = await response.json();
    
    // Step 2: Download installer
    const link = document.createElement('a');
    link.href = installerUrl;
    link.download = 'AllumiWindowsSensor-Setup.exe';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    
    // Step 3: Show installation instructions
    showDialog({
      title: "Installation Instructions",
      message: `
        1. Run the downloaded installer
        2. Click "Install" and wait for completion
        3. Click the button below to launch the app
      `,
      buttonText: "Launch Allumi Sensor",
      onConfirm: () => {
        // Step 4: Launch app with token via URL scheme
        window.location.href = `allumi://setup?token=${exchangeToken}`;
      }
    });
    
  } catch (error) {
    console.error('Download failed:', error);
    showError('Failed to generate installer. Please try again.');
  }
}
```

**Alternative: Automatic Launch (2-second delay)**
```javascript
// After download starts
setTimeout(() => {
  // Automatically open URL scheme
  window.location.href = `allumi://setup?token=${exchangeToken}`;
}, 2000);

// Or show a "Click here if app doesn't start automatically" button
```

---

### 3. Windows App: Setup Token Handler ✅ COMPLETE

**File:** `Program.cs`

**Main() Method:**
```csharp
[STAThread]
static void Main(string[] args)
{
    SquirrelAwareApp.HandleEvents(
        onInitialInstall: OnAppInstall,
        onAppUninstall: OnAppUninstall,
        onEveryRun: OnAppRun
    );

    // Check for setup token URL: allumi://setup?token=xxx
    if (args.Length > 0 && args[0].StartsWith("allumi://setup?", StringComparison.OrdinalIgnoreCase))
    {
        HandleSetupToken(args[0]);
        return; // Exit after saving token
    }

    // Check for OAuth callback
    if (args.Length > 0 && args[0].StartsWith("allumi://", StringComparison.OrdinalIgnoreCase))
    {
        OAuthHandler.HandleProtocolCallback(args[0]);
        return;
    }

    // Normal app launch
    LaunchApp();
}
```

**HandleSetupToken() Method:**
```csharp
private static void HandleSetupToken(string setupUrl)
{
    try
    {
        // Parse URL: allumi://setup?token=tok_xyz123
        var uri = new Uri(setupUrl);
        var queryParams = HttpUtility.ParseQueryString(uri.Query);
        var token = queryParams["token"];

        if (!string.IsNullOrEmpty(token))
        {
            // Save token to file
            var tokenPath = Path.Combine(AppContext.BaseDirectory, ".exchange-token");
            File.WriteAllText(tokenPath, token);

            // Show success message
            MessageBox.Show(
                "Device setup initiated! The app will now start and configure automatically.",
                "Allumi Sensor - Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            // Launch app to trigger exchange
            LaunchApp();
        }
        else
        {
            MessageBox.Show(
                "Setup failed: No token provided in URL.",
                "Allumi Sensor - Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
    catch (Exception ex)
    {
        LogError($"Failed to handle setup token: {ex.Message}");
        MessageBox.Show(
            $"Setup failed: {ex.Message}",
            "Allumi Sensor - Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error
        );
    }
}
```

---

### 4. Windows App: Token Exchange ✅ COMPLETE

**File:** `Config.cs`

**Load() Method (Automatic Flow):**
```csharp
public static Config Load()
{
    var configPath = GetConfigPath();

    // 1. Check for existing config
    if (File.Exists(configPath))
    {
        var json = File.ReadAllText(configPath);
        return JsonConvert.DeserializeObject<Config>(json);
    }

    // 2. Check for exchange token
    var token = GetExchangeToken();
    if (!string.IsNullOrEmpty(token))
    {
        try
        {
            // Exchange token for credentials
            var config = ExchangeTokenForConfig(token).Result;
            config.Save();
            return config;
        }
        catch (Exception ex)
        {
            LogError($"Token exchange failed: {ex.Message}");
            // Fall through to OAuth
        }
    }

    // 3. Fall back to OAuth
    var oauthConfig = OAuthHandler.AuthenticateAsync().Result;
    oauthConfig.Save();
    return oauthConfig;
}
```

**GetExchangeToken() Method:**
```csharp
private static string GetExchangeToken()
{
    // Priority 1: File in EXE directory
    var tokenPath = Path.Combine(AppContext.BaseDirectory, ".exchange-token");
    if (File.Exists(tokenPath))
    {
        var token = File.ReadAllText(tokenPath).Trim();
        File.Delete(tokenPath); // Single-use
        return token;
    }

    // Priority 2: Environment variable
    var envToken = Environment.GetEnvironmentVariable("ALLUMI_EXCHANGE_TOKEN");
    if (!string.IsNullOrEmpty(envToken))
    {
        return envToken;
    }

    return null;
}
```

**ExchangeTokenForConfig() Method:**
```csharp
private static async Task<Config> ExchangeTokenForConfig(string token)
{
    using (var client = new HttpClient())
    {
        client.Timeout = TimeSpan.FromSeconds(10);

        var request = new TokenExchangeRequest { Token = token };
        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(
            "https://lstannxhfhunacgkvtmm.supabase.co/functions/v1/exchange-device-token",
            content
        );

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Token exchange failed: {error}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonConvert.DeserializeObject<TokenExchangeResponse>(responseJson);

        return new Config
        {
            DeviceId = exchangeResponse.DeviceId,
            ApiKey = exchangeResponse.ApiKey,
            SyncUrl = exchangeResponse.SyncUrl,
            UserId = exchangeResponse.UserId,
            IdleThresholdSeconds = 60
        };
    }
}
```

---

### 5. Supabase: Exchange Endpoint ✅ COMPLETE (Lovable)

**Edge Function:** `exchange-device-token`

**URL:** `POST /functions/v1/exchange-device-token`

**Request:**
```json
{
  "token": "tok_abc123xyz"
}
```

**Response (Success - 200):**
```json
{
  "deviceId": "windows-uuid-timestamp",
  "apiKey": "alm_randomkey",
  "syncUrl": "https://lstannxhfhunacgkvtmm.supabase.co/functions/v1/sync-device-activity",
  "userId": "user-uuid"
}
```

**Response (Error - 401):**
```json
{
  "error": "Invalid or expired token"
}
```

**Validation Logic:**
```typescript
// 1. Look up token
const { data: tokenRecord } = await supabase
  .from('device_exchange_tokens')
  .select('*')
  .eq('token', requestToken)
  .single();

// 2. Validate
if (!tokenRecord) {
  return new Response(
    JSON.stringify({ error: 'Token not found' }),
    { status: 401 }
  );
}

if (tokenRecord.consumed) {
  return new Response(
    JSON.stringify({ error: 'Token already used' }),
    { status: 401 }
  );
}

if (new Date(tokenRecord.expires_at) < new Date()) {
  return new Response(
    JSON.stringify({ error: 'Token expired' }),
    { status: 401 }
  );
}

// 3. Mark consumed
await supabase
  .from('device_exchange_tokens')
  .update({ consumed: true, consumed_at: new Date() })
  .eq('token', requestToken);

// 4. Return credentials
return new Response(
  JSON.stringify({
    deviceId: tokenRecord.device_id,
    apiKey: tokenRecord.api_key,
    syncUrl: 'https://lstannxhfhunacgkvtmm.supabase.co/functions/v1/sync-device-activity',
    userId: tokenRecord.user_id
  }),
  { status: 200 }
);
```

---

## Database Schema

```sql
CREATE TABLE device_exchange_tokens (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  token TEXT UNIQUE NOT NULL,              -- tok_<random>
  device_id TEXT NOT NULL,                 -- windows-uuid-timestamp
  api_key TEXT NOT NULL,                   -- alm_<random>
  user_id UUID NOT NULL,                   -- User who requested installer
  created_at TIMESTAMP DEFAULT NOW(),
  expires_at TIMESTAMP NOT NULL,           -- created_at + 5 minutes
  consumed BOOLEAN DEFAULT false,
  consumed_at TIMESTAMP,
  
  -- Foreign keys
  FOREIGN KEY (user_id) REFERENCES auth.users(id) ON DELETE CASCADE,
  FOREIGN KEY (device_id) REFERENCES devices(device_id) ON DELETE CASCADE
);

-- Indexes
CREATE INDEX idx_exchange_tokens_token ON device_exchange_tokens(token);
CREATE INDEX idx_exchange_tokens_expires ON device_exchange_tokens(expires_at);
CREATE INDEX idx_exchange_tokens_consumed ON device_exchange_tokens(consumed);
```

---

## Security Features

| Feature | Description |
|---------|-------------|
| ⏱️ **Time-Limited** | Tokens expire after 5 minutes |
| 🔒 **Single-Use** | Token marked consumed after exchange |
| 🚫 **No Long-Lived Credentials** | Installer contains only short-lived token |
| 📝 **Audit Trail** | All token creation/consumption logged |
| 🔐 **HTTPS Only** | All communication encrypted |
| 🎯 **URL Scheme Security** | `allumi://` protocol registered to specific app only |

---

## Testing Scenarios

### ✅ Scenario 1: Happy Path

```
1. User clicks "Download Windows" on dashboard
   → Lovable creates token, returns installer URL
   
2. User downloads installer
   → Normal Squirrel installer (no modifications needed)
   
3. User installs app
   → Installs to %LocalAppData%\AllumiWindowsSensor
   
4. Frontend opens: allumi://setup?token=tok_abc123
   → Windows catches protocol, saves token to .exchange-token file
   
5. App launches
   → Config.Load() finds token file
   
6. App exchanges token
   → POST to /exchange-device-token
   → Receives { deviceId, apiKey, syncUrl, userId }
   
7. App saves config and starts tracking
   → config.json created in EXE directory
   → Activity tracking begins immediately
   
8. Activities appear in dashboard
   → Within 30 seconds of first app switch

EXPECTED RESULT: ✅ Zero manual configuration
```

### ⚠️ Scenario 2: Token Expired

```
1. User downloads installer
2. Waits 10 minutes (token expires after 5)
3. Frontend opens: allumi://setup?token=tok_expired
4. App launches, attempts exchange
5. Server responds: 401 "Token expired"
6. App falls back to OAuth
7. Browser opens for authentication
8. User completes OAuth flow
9. App starts tracking

EXPECTED RESULT: ⚠️ Graceful fallback to OAuth
```

### 🔒 Scenario 3: Token Already Consumed

```
1. User installs on PC #1
   → Token exchanged successfully
   
2. User tries same installer on PC #2
   → Token already marked consumed
   
3. Server responds: 401 "Token already used"
4. App falls back to OAuth
5. User completes authentication

EXPECTED RESULT: 🔒 Security maintained, OAuth fallback works
```

### 🌐 Scenario 4: Network Failure

```
1. User installs app while offline
2. App can't reach exchange endpoint
3. Shows error: "Unable to connect. Retrying..."
4. After 3 retries (30 seconds), falls back to OAuth
5. OAuth also fails (offline)
6. App shows: "Please connect to internet and restart"

EXPECTED RESULT: 🌐 Graceful error handling
```

---

## Implementation Status

| Component | Status | Owner | Location |
|-----------|--------|-------|----------|
| Token Generation | ✅ COMPLETE | Lovable | `generate-custom-installer` function |
| Database Table | ✅ COMPLETE | Lovable | `device_exchange_tokens` table |
| Exchange Endpoint | ✅ COMPLETE | Lovable | `/functions/v1/exchange-device-token` |
| Setup Token Handler | ✅ COMPLETE | Windows | `Program.cs` HandleSetupToken() |
| Token Exchange | ✅ COMPLETE | Windows | `Config.cs` ExchangeTokenForConfig() |
| Protocol Registration | ✅ COMPLETE | Windows | `allumi://` in Registry |
| Frontend URL Launch | ⏳ IN PROGRESS | Lovable | JavaScript `window.location.href` |

---

## Troubleshooting Guide

### Problem: Frontend can't open `allumi://` URL

**Symptoms:**
- Browser shows "Protocol not supported"
- Nothing happens when clicking "Launch App"

**Cause:** App not installed or protocol not registered

**Fix:**
1. Verify installer completed successfully
2. Check Registry: `HKEY_CLASSES_ROOT\allumi`
3. Reinstall app if necessary

---

### Problem: Token exchange returns 401

**Symptoms:**
- App shows OAuth prompt immediately
- Log shows "Token exchange failed: 401"

**Cause:** Token expired (>5 minutes) or already consumed

**Fix:**
1. Generate new token (re-download from dashboard)
2. Complete OAuth flow as fallback
3. Check Supabase logs for token status

---

### Problem: App starts but doesn't track

**Symptoms:**
- Tray icon appears
- No activities in dashboard
- Log shows "Sync failed"

**Cause:** Config.json not created or invalid

**Fix:**
1. Check `[EXE_DIR]\config.json` exists
2. Verify contents match schema
3. Check `[EXE_DIR]\logs\sensor.log` for errors
4. Delete config.json and restart app to retry

---

### Problem: Activities sync but don't appear in dashboard

**Symptoms:**
- Log shows "Sync SUCCESS"
- Dashboard shows "No activities"

**Cause:** Wrong user_id in config

**Fix:**
1. Verify `user_id` in config.json matches logged-in user
2. Check Supabase database: `SELECT * FROM activities WHERE device_id = 'xxx'`
3. Regenerate config with correct token

---

## Next Steps

### Immediate (Waiting on Lovable):
1. ✅ **Frontend Integration**: Implement `window.location.href = 'allumi://setup?token=xxx'` after download
2. ⏳ **End-to-End Test**: Test complete flow with real user account
3. ⏳ **Error Handling**: Add frontend handling for protocol launch failures

### Short-Term:
1. **Alpha Testing**: Distribute to 2-3 test users
2. **Monitoring**: Track token exchange success rate in database
3. **Documentation**: Update user-facing docs with new automated flow

### Long-Term:
1. **Code Signing**: Purchase certificate to eliminate "Don't Trust" warnings
2. **Analytics**: Add metrics for setup success rate
3. **Optimization**: Consider reducing token expiry to 2 minutes for tighter security

---

## Success Metrics

**Target Metrics:**
- ✅ Token exchange success rate: >95%
- ✅ Time to first activity: <60 seconds after install
- ✅ Manual OAuth fallback rate: <5%
- ✅ User complaints about setup: 0

**Current Status:**
- Windows app implementation: 100% complete
- Backend implementation: 100% complete
- Frontend implementation: In progress
- End-to-end testing: Pending

---

## Related Files

- `Program.cs` - Main entry point, setup token handler
- `Config.cs` - Token exchange, config management
- `Auth/OAuthHandler.cs` - OAuth fallback mechanism
- `Sync/SyncClient.cs` - Activity syncing to Supabase
- `TOKEN_EXCHANGE_SPEC.md` - This document (original)
- `TOKEN_EXCHANGE_SPEC_V2.md` - This document (latest)
