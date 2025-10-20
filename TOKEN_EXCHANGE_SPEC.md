# Token Exchange System - Integration Spec

## Overview

The Windows Sensor app now supports automatic credential provisioning via a secure token exchange system using URL-based token delivery.

## How It Works

### 1. User Downloads Installer (Lovable Side)

When user clicks "Download Windows" on the dashboard:

```typescript
// generate-custom-installer edge function responds with:
{
  "installerUrl": "https://github.com/fini-commits/Allumi.WindowsSensor/releases/latest/download/AllumiWindowsSensor-Setup.exe",
  "exchangeToken": "tok_xyz123",
  "deviceId": "windows-xxx",
  "deviceName": "User's Windows PC"
}
```

**Database:**
```sql
INSERT INTO device_exchange_tokens (
  token, device_id, api_key, user_id, expires_at
) VALUES (
  'tok_xyz123',
  'windows-xxx',
  'alm_p73ba3...',
  'user-guid',
  NOW() + INTERVAL '5 minutes'
);
```

### 2. Frontend Downloads Installer and Launches Setup

```javascript
// Frontend code after user clicks download:
const { installerUrl, exchangeToken, deviceId } = await response.json();

// Download installer
const installerBlob = await fetch(installerUrl);
const url = window.URL.createObjectURL(installerBlob);

// Trigger download
const a = document.createElement('a');
a.href = url;
a.download = 'AllumiWindowsSensor-Setup.exe';
a.click();

// After user installs (either wait or user confirms)
setTimeout(() => {
  // Launch app with token via custom URL scheme
  window.location.href = `allumi://setup?token=${exchangeToken}`;
}, 2000); // Or show "Click here after installing"
```

### 3. Windows App Receives Setup Token (Windows App - IMPLEMENTED ✅)

```csharp
// In Program.cs Main():
if (args.Length > 0 && args[0].StartsWith("allumi://setup?", StringComparison.OrdinalIgnoreCase))
{
    // Extract token: allumi://setup?token=tok_xyz123
    var token = ExtractTokenFromUrl(args[0]);
    
    // Save to .exchange-token file
    File.WriteAllText("[EXE_DIR]/.exchange-token", token);
    
    // Show success message
    MessageBox.Show("Device setup initiated! Configuring...");
    
    // Launch app normally
    LaunchApp();
}
```

### 4. Token Exchange Endpoint (Lovable Side - TODO)

Create Supabase Edge Function: `exchange-device-token`

**Endpoint:** `POST /functions/v1/exchange-device-token`

**Request:**
```json
{
  "token": "tok_xyz123"
}
```

**Response (Success - 200):**
```json
{
  "deviceId": "windows-xxx",
  "deviceName": "User's Windows PC",
  "apiKey": "alm_p73ba3...",
  "syncUrl": "https://lstannxhfhunacgkvtmm.supabase.co/functions/v1/sync-device-activity",
  "userId": "user-guid",
  "idleThresholdSeconds": 60
}
```

**Response (Error - 401):**
```json
{
  "error": "Invalid token",
  "message": "Token not found, expired, or already consumed"
}
```

**Validation Logic:**
```typescript
1. Lookup token in device_exchange_tokens table
2. Check: token exists
3. Check: expires_at > now() (not expired)
4. Check: consumed = false (not already used)
5. If all checks pass:
   - Mark token as consumed (UPDATE consumed = true)
   - Return device credentials
6. Otherwise:
   - Return 401 error
```

## Database Schema

### New Table: `device_exchange_tokens`

```sql
CREATE TABLE device_exchange_tokens (
  id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  token TEXT UNIQUE NOT NULL,
  device_id TEXT NOT NULL REFERENCES devices(device_id),
  api_key TEXT NOT NULL,
  user_id UUID NOT NULL REFERENCES auth.users(id),
  expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
  consumed BOOLEAN DEFAULT false,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
  consumed_at TIMESTAMP WITH TIME ZONE
);

CREATE INDEX idx_exchange_tokens_token ON device_exchange_tokens(token);
CREATE INDEX idx_exchange_tokens_expires ON device_exchange_tokens(expires_at);
```

## Security Features

✅ **Time-Limited:** Tokens expire after 5 minutes
✅ **Single-Use:** Token marked as consumed after exchange
✅ **Secure:** Token only works once, can't be reused
✅ **No Credentials in Installer:** Installer only contains short-lived token
✅ **Audit Trail:** Track when tokens are created and consumed

## Testing Flow

### Happy Path:
1. User clicks "Download Windows" → Token created
2. User downloads installer (contains token)
3. User installs app
4. App starts → Reads `.exchange-token` file
5. App calls exchange endpoint
6. Receives credentials
7. Saves to config.json
8. Starts tracking immediately ✅

### Token Expired:
1. User downloads installer
2. Waits 6 minutes
3. Installs app
4. Token expired → Exchange fails
5. App shows OAuth prompt
6. User authenticates via browser ✅

### Token Already Used:
1. User installs on PC #1 → Token consumed
2. User tries to install on PC #2 with same installer
3. Token already consumed → Exchange fails
4. App shows OAuth prompt ✅

## Implementation Status

### ✅ Completed (Windows App):
- Token file reading (`.exchange-token`)
- Environment variable fallback
- HTTP exchange call to Supabase
- Config saving after exchange
- Single-use token deletion
- Fallback to OAuth if exchange fails

### ⏳ Pending (Lovable):
1. Create `device_exchange_tokens` table
2. Modify `generate-custom-installer` to create tokens
3. Package `.exchange-token` file with installer
4. Create `exchange-device-token` edge function
5. Test full flow

## Next Steps

**Lovable should:**
1. Implement token exchange endpoint
2. Update installer generation to include token
3. Test with new user install

**After Lovable completes their side:**
1. Test full flow with real user
2. Verify token exchange works
3. Confirm OAuth fallback works if token expires
4. Release to production! 🚀
