# 🔗 Lovable Integration Guide - Final Configuration

## ✅ What's Been Implemented (Windows Sensor Side)

All the Windows Sensor code is ready! Here's what was built:

1. **Embedded Config Reader** - Extracts config from .exe
2. **Windows Credential Manager** - Securely stores API keys
3. **OAuth Deep Link Handler** - Browser authentication fallback
4. **Smart Config Loading** - Multi-level fallback system
5. **GitHub Release Automation** - Automatic release creation

## 🎯 Critical Configuration for Vetra

### **1. Update the GitHub URL in Vetra Edge Function**

In `supabase/functions/generate-custom-installer/index.ts` line 82:

```typescript
// CHANGE THIS:
const baseInstallerUrl = 'https://github.com/YOUR_GITHUB_USERNAME/Allumi.WindowsSensor/releases/latest/download/Setup.exe';

// TO THIS:
const baseInstallerUrl = 'https://github.com/fini-commits/Allumi.WindowsSensor/releases/latest/download/Setup.exe';
```

### **2. Config Embedding Format**

The edge function should append config like this:

```typescript
// Pseudo-code for what Vetra edge function does:
const configJson = JSON.stringify({
  apiKey: "alm_xxxxx",
  userId: "user-uuid",
  deviceId: "windows-uuid-timestamp",
  deviceName: "User's PC",
  syncUrl: "https://lstannxhfhunacgkvtmm.supabase.co/functions/v1/sync-device-activity",
  supabaseUrl: "https://lstannxhfhunacgkvtmm.supabase.co",
  supabaseAnonKey: "eyJhbGci..."
});

// Append to exe binary
const customExe = Buffer.concat([
  baseExeBuffer,
  Buffer.from("<<<ALLUMI_CONFIG>>>", "utf-8"),
  Buffer.from(configJson, "utf-8"),
  Buffer.from("<<<END_CONFIG>>>", "utf-8")
]);
```

### **3. OAuth Callback URL (Fallback)**

If user downloads generic installer, they'll be redirected to:
```
https://your-vetra-domain.com/auth/device-login?device_id=xxx&callback=allumi://auth
```

After login, redirect to:
```
allumi://auth?config={urlEncodedConfigJson}
```

Or use local callback:
```
http://localhost:42813/?config={urlEncodedConfigJson}
```

## 🚀 How to Test

### **Step 1: Build the Base Installer**

1. Go to: https://github.com/fini-commits/Allumi.WindowsSensor/actions
2. Click "Build Windows Sensor (Squirrel)"
3. Click "Run workflow" → Select "main" branch → Run
4. Wait ~3-4 minutes for build
5. Check "Releases" tab - new release should be created with `Setup.exe`

### **Step 2: Test Vetra Integration**

1. User logs into Vetra dashboard
2. User clicks "Download Windows Sensor"
3. Vetra edge function:
   - Downloads `Setup.exe` from GitHub releases
   - Generates user's config
   - Embeds config into exe
   - Returns custom installer
4. User runs custom installer
5. **Expected**: App auto-configures and starts tracking! ✅

### **Step 3: Test OAuth Fallback**

1. User downloads generic `Setup.exe` directly from GitHub
2. Runs installer
3. App shows "Click to authenticate" notification
4. User clicks → Browser opens
5. User logs in to Vetra
6. Browser redirects to `allumi://auth`
7. App receives config and completes setup

## 📋 Config JSON Format

The Windows Sensor expects this structure:

```json
{
  "apiKey": "alm_xxxxxxxxxxxxx",
  "userId": "user-uuid-here",
  "deviceId": "windows-{userId}-{timestamp}",
  "deviceName": "User's Windows PC",
  "syncUrl": "https://lstannxhfhunacgkvtmm.supabase.co/functions/v1/sync-device-activity",
  "supabaseUrl": "https://lstannxhfhunacgkvtmm.supabase.co",
  "supabaseAnonKey": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Required fields:**
- `apiKey` - Device API key (alm_ prefix)
- `deviceId` - Unique device identifier
- `deviceName` - Human-readable device name
- `syncUrl` - URL to sync activities

**Optional fields:**
- `userId` - User UUID (for tracking)
- `supabaseUrl` - Supabase project URL
- `supabaseAnonKey` - Supabase anon key

## 🔒 Security Notes

1. **API Key Storage**: Automatically stored in Windows Credential Manager (encrypted)
2. **Config Persistence**: Saved to `%AppData%\Allumi\config.json` after extraction
3. **Protocol Handler**: Registered to `HKEY_CURRENT_USER` (per-user, not system-wide)

## 🐛 Troubleshooting

### Issue: "No embedded config found"
- **Cause**: Config wasn't properly appended to exe
- **Fix**: Check edge function is using exact markers: `<<<ALLUMI_CONFIG>>>` and `<<<END_CONFIG>>>`

### Issue: "Authentication failed or timed out"
- **Cause**: OAuth callback not working
- **Fix**: Ensure browser can access `allumi://` protocol or `http://localhost:42813`

### Issue: Setup.exe not found in releases
- **Cause**: GitHub Actions workflow hasn't run yet
- **Fix**: Manually trigger workflow or wait for next commit

## 📞 Integration Checklist

- [ ] Update GitHub URL in Vetra edge function
- [ ] Test downloading base installer from releases
- [ ] Test config embedding works
- [ ] Test custom installer auto-configures app
- [ ] Test OAuth fallback flow
- [ ] Update Vetra UI to remove ZIP references
- [ ] Deploy edge function to production

## 🎉 Success Criteria

When everything works correctly:

1. ✅ User clicks one button in Vetra dashboard
2. ✅ Single .exe downloads (not ZIP)
3. ✅ User runs exe → No prompts, no config files
4. ✅ App starts tracking immediately
5. ✅ Activities appear in Vetra dashboard
6. ✅ Device shows as "Online" in device list

---

**Repository**: https://github.com/fini-commits/Allumi.WindowsSensor  
**Latest Release**: https://github.com/fini-commits/Allumi.WindowsSensor/releases/latest  
**Base Installer**: https://github.com/fini-commits/Allumi.WindowsSensor/releases/latest/download/Setup.exe
