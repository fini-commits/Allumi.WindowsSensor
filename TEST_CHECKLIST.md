# End-to-End Testing Checklist

**Date:** October 21, 2025  
**Status:** Ready to test! ✅  
**Alignment:** Windows App + Lovable Backend + Lovable Frontend = 100% aligned

---

## ✅ Pre-Test Verification

### Windows App Side (YOUR SIDE)
- [x] `HandleSetupToken()` method implemented in `Program.cs`
- [x] Parses `allumi://setup?token=xxx` URLs
- [x] Saves token to `.exchange-token` file
- [x] `Config.Load()` reads token and exchanges for credentials
- [x] Falls back to OAuth if token exchange fails
- [x] Protocol `allumi://` registered in Windows Registry
- [x] Build successful (latest: commit `460ff6c`)

### Backend Side (LOVABLE)
- [x] `generate-custom-installer` returns `{ installerUrl, exchangeToken }`
- [x] Database table `device_exchange_tokens` created
- [x] Exchange endpoint `/functions/v1/exchange-device-token` deployed
- [x] Token validation logic (expiry, consumed check)

### Frontend Side (LOVABLE - JUST COMPLETED)
- [x] Downloads normal installer from GitHub
- [x] Returns `exchangeToken` to frontend
- [x] Opens `allumi://setup?token=${exchangeToken}` after installation

---

## 🧪 Test Procedure

### Test 1: Happy Path - New User Installation

**Objective:** Verify zero manual configuration

**Steps:**

1. **Login to Allumi Dashboard**
   - [ ] Open dashboard in browser
   - [ ] Login with test account

2. **Download Installer**
   - [ ] Click "Download Windows Tracker" button
   - [ ] Frontend calls `generate-custom-installer`
   - [ ] Check browser console: Should see `{ installerUrl, exchangeToken }`
   - [ ] Verify token format: `tok_<random>` (e.g., `tok_abc123xyz`)
   - [ ] Installer downloads automatically

3. **Install Application**
   - [ ] Run downloaded installer: `AllumiWindowsSensor-Setup.exe`
   - [ ] Click "Install"
   - [ ] Wait for completion (~10 seconds)
   - [ ] Installer closes

4. **Launch App with Token**
   - [ ] Frontend shows dialog: "Click OK after installing"
   - [ ] User clicks OK
   - [ ] Frontend executes: `window.location.href = 'allumi://setup?token=tok_xxx'`
   - [ ] Windows app launches
   - [ ] MessageBox appears: "Device setup initiated! The app will now start and configure automatically."

5. **Verify Token Exchange**
   - [ ] App closes MessageBox
   - [ ] App restarts normally
   - [ ] Check file: `%LocalAppData%\AllumiWindowsSensor\app-{version}\.exchange-token`
     - Should be **deleted** after exchange
   - [ ] Check file: `%LocalAppData%\AllumiWindowsSensor\app-{version}\config.json`
     - Should be **created** with credentials

6. **Verify Tracking Started**
   - [ ] Tray icon appears in system tray
   - [ ] Switch between apps (Chrome, VS Code, etc.)
   - [ ] Wait 30 seconds

7. **Verify Activities in Dashboard**
   - [ ] Refresh dashboard
   - [ ] Activities appear in timeline
   - [ ] Correct app names, durations, categories

**Expected Result:** ✅ Zero manual configuration, tracking starts immediately

**If Fails:** Check logs in `%LocalAppData%\AllumiWindowsSensor\app-{version}\logs\sensor.log`

---

### Test 2: Token Expiry - Fallback to OAuth

**Objective:** Verify graceful fallback when token expires

**Steps:**

1. **Generate Token**
   - [ ] Click "Download Windows Tracker"
   - [ ] Note the `exchangeToken` value
   - [ ] Download installer

2. **Wait for Expiry**
   - [ ] Wait **6 minutes** (tokens expire after 5 minutes)
   - [ ] Do NOT install yet

3. **Install After Expiry**
   - [ ] Run installer
   - [ ] Frontend opens: `allumi://setup?token=tok_expired`
   - [ ] App saves token to file
   - [ ] App launches

4. **Verify Fallback**
   - [ ] App attempts token exchange
   - [ ] Exchange fails: 401 "Token expired"
   - [ ] Browser opens for OAuth
   - [ ] User completes authentication

5. **Verify Tracking**
   - [ ] Config created via OAuth
   - [ ] Activities appear in dashboard

**Expected Result:** ⚠️ Graceful fallback to OAuth, no data loss

---

### Test 3: Protocol Registration - Fresh Windows Install

**Objective:** Verify protocol works on clean system

**Steps:**

1. **Check Registry Before Install**
   - [ ] Open Registry Editor
   - [ ] Navigate to: `HKEY_CLASSES_ROOT\allumi`
   - [ ] Verify: Key does NOT exist

2. **Install App**
   - [ ] Run installer
   - [ ] Complete installation

3. **Check Registry After Install**
   - [ ] Refresh Registry Editor
   - [ ] Navigate to: `HKEY_CLASSES_ROOT\allumi`
   - [ ] Verify: Key exists
   - [ ] Check value: Points to `Allumi.WindowsSensor.exe`

4. **Test Protocol Launch**
   - [ ] Open PowerShell
   - [ ] Run: `Start-Process "allumi://setup?token=test-123"`
   - [ ] App launches
   - [ ] Token saved to `.exchange-token` file

**Expected Result:** ✅ Protocol registered automatically

---

### Test 4: Network Failure - Offline Install

**Objective:** Verify error handling when offline

**Steps:**

1. **Disconnect Internet**
   - [ ] Turn off WiFi / disconnect Ethernet

2. **Download and Install**
   - [ ] (Download installer while online first)
   - [ ] Run installer while offline
   - [ ] Complete installation

3. **Launch with Token**
   - [ ] Frontend opens: `allumi://setup?token=tok_xxx`
   - [ ] App launches
   - [ ] App attempts token exchange
   - [ ] Exchange fails: Network error

4. **Verify Retry**
   - [ ] App shows: "Unable to connect. Retrying..."
   - [ ] After 3 retries, falls back to OAuth
   - [ ] OAuth also fails (offline)

5. **Reconnect and Restart**
   - [ ] Reconnect internet
   - [ ] Close app
   - [ ] Restart app from Start Menu
   - [ ] Token exchange succeeds

**Expected Result:** 🌐 Graceful error handling, retry mechanism works

---

## 🔍 Validation Points

### After Each Test:

**Database Checks (Supabase):**
```sql
-- Check token was created
SELECT * FROM device_exchange_tokens 
WHERE token = 'tok_xxx';

-- Verify token was consumed
SELECT consumed, consumed_at 
FROM device_exchange_tokens 
WHERE token = 'tok_xxx';

-- Check device was created
SELECT * FROM devices 
WHERE device_id = 'windows-xxx';

-- Verify activities are syncing
SELECT * FROM activities 
WHERE device_id = 'windows-xxx'
ORDER BY timestamp DESC 
LIMIT 10;
```

**Windows App Checks:**
```powershell
# Check config file exists
Get-Content "$env:LOCALAPPDATA\AllumiWindowsSensor\app-*\config.json"

# Check logs for errors
Get-Content "$env:LOCALAPPDATA\AllumiWindowsSensor\app-*\logs\sensor.log" -Tail 20

# Check sync logs
Get-Content "$env:LOCALAPPDATA\AllumiWindowsSensor\app-*\logs\sync.log" -Tail 20

# Verify token file was deleted
Test-Path "$env:LOCALAPPDATA\AllumiWindowsSensor\app-*\.exchange-token"
# Should return: False
```

**Frontend Checks:**
```javascript
// Browser console should show:
{
  installerUrl: "https://github.com/fini-commits/Allumi.WindowsSensor/releases/latest/download/AllumiWindowsSensor-Setup.exe",
  exchangeToken: "tok_abc123xyz",
  deviceId: "windows-uuid-timestamp",
  deviceName: "User's Windows PC"
}
```

---

## 🐛 Common Issues & Fixes

### Issue: "Protocol not supported" in browser

**Cause:** App not installed or protocol not registered

**Fix:**
1. Verify installer completed successfully
2. Check Registry: `HKEY_CLASSES_ROOT\allumi`
3. Reinstall if necessary

---

### Issue: Token exchange returns 401

**Cause:** Token expired or already consumed

**Fix:**
1. Check token age (should be <5 minutes old)
2. Check `device_exchange_tokens` table for `consumed = true`
3. Generate new token (re-download)
4. Or use OAuth fallback

---

### Issue: App launches but doesn't track

**Cause:** Config not created or invalid

**Fix:**
1. Check `config.json` exists in app directory
2. Verify JSON structure matches schema
3. Check logs for sync errors
4. Delete config and restart to retry

---

### Issue: Activities don't appear in dashboard

**Cause:** Wrong `user_id` in config

**Fix:**
1. Check `user_id` in `config.json`
2. Compare with logged-in user in dashboard
3. Query database: `SELECT * FROM activities WHERE device_id = 'xxx'`
4. Regenerate config with correct token

---

## 📊 Success Criteria

**Test 1 (Happy Path):**
- ✅ Zero manual configuration
- ✅ Activities appear within 60 seconds
- ✅ Token consumed in database
- ✅ Config file created automatically

**Test 2 (Token Expiry):**
- ✅ Graceful fallback to OAuth
- ✅ User can still authenticate
- ✅ No app crashes

**Test 3 (Protocol Registration):**
- ✅ Registry key created automatically
- ✅ Custom URL launches app correctly

**Test 4 (Network Failure):**
- ✅ Retry mechanism works
- ✅ Clear error messages
- ✅ Recovery after reconnect

---

## 🎯 Next Steps After Testing

### If All Tests Pass ✅
1. [ ] Document successful flow
2. [ ] Start alpha testing with 2-3 users
3. [ ] Monitor token exchange success rate
4. [ ] Gather user feedback

### If Tests Fail ❌
1. [ ] Document exact failure point
2. [ ] Check logs (Windows + Supabase)
3. [ ] Verify alignment between frontend/backend/app
4. [ ] Fix issues and re-test

---

## 📝 Test Results Template

```
Test Date: _______________
Tester: _______________
Windows Version: _______________
.NET Version: _______________

Test 1 (Happy Path): [ ] PASS  [ ] FAIL
Notes: _______________________________________

Test 2 (Token Expiry): [ ] PASS  [ ] FAIL
Notes: _______________________________________

Test 3 (Protocol Registration): [ ] PASS  [ ] FAIL
Notes: _______________________________________

Test 4 (Network Failure): [ ] PASS  [ ] FAIL
Notes: _______________________________________

Overall Result: [ ] READY FOR ALPHA  [ ] NEEDS FIXES
```

---

**You're aligned and ready to test! Good luck! 🚀**
