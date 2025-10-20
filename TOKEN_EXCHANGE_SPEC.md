# Token Exchange System - Integration Spec

## Overview

The Windows Sensor app now supports automatic credential provisioning via a secure token exchange system. This eliminates the need to embed credentials in the installer.

## How It Works

### 1. User Downloads Installer (Lovable Side)

When user clicks "Download Windows" on the dashboard:

```typescript
// generate-custom-installer edge function
1. Create device record in database
2. Generate API key: alm_<random>
3. Create exchange token: tok_<random> (expires in 5 minutes)
4. Store in device_exchange_tokens table:
   {
     token: "tok_xyz123",
     device_id: "windows-xxx",
     api_key: "alm_p73ba3...",
     user_id: "user-guid",
     expires_at: now() + 5 minutes,
     consumed: false
   }
5. Download base installer from GitHub
6. Create .exchange-token file with token
7. Package .exchange-token with installer (see below)
8. Return modified installer to user
```

### 2. Installer Extracts Token (Installer Side)

The installer should extract `.exchange-token` file to:
```
%LocalAppData%\AllumiWindowsSensor\app-{version}\.exchange-token
```

**Methods to achieve this:**

**Option A: Squirrel Package Modification**
```bash
# Extract .nupkg from installer
# Add .exchange-token to package
# Repackage and sign
```

**Option B: Post-Install Script**
```bash
# After Squirrel installs, run script that creates .exchange-token
```

**Option C: Environment Variable**
```bash
# Set ALLUMI_EXCHANGE_TOKEN environment variable
# App will read from there if file doesn't exist
```

### 3. App Exchanges Token on First Launch (Windows App)

```csharp
// Config.cs - Already implemented!
1. Check if config.json exists
   - If yes: load and use ✅
   - If no: continue to step 2

2. Check for exchange token:
   - Read from: [EXE_DIR]\.exchange-token
   - OR from environment variable: ALLUMI_EXCHANGE_TOKEN
   
3. If token found:
   - POST to: /functions/v1/exchange-device-token
   - Body: { "token": "tok_xyz123" }
   - Receive config JSON
   - Save to config.json
   - Delete .exchange-token file (single-use)
   - Start tracking ✅

4. If no token or exchange fails:
   - Show OAuth prompt
   - User authenticates via browser
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
