# Release Notes - v1.0.21

## 🎉 What's New

### Version Display
- **System Tray**: Now shows version number in the tooltip (e.g., "Allumi Sensor v1.0.21 • Tracking")
- **Context Menu**: Added "Version 1.0.21" menu item to easily check which version is running
- **Update Awareness**: You can always see what version you have installed

### Update Notifications
- **Auto-Check on Startup**: App automatically checks for updates when it starts
- **Manual Check**: Added "Check for Updates" option in the system tray context menu
- **User Prompts**: When an update is available, you'll see a dialog:
  - Shows current version vs new version
  - "Yes" to update immediately (app will restart)
  - "No" to skip this update
- **No More Manual Downloads**: After this ONE final install, future updates happen automatically!

### Installer Improvements
- **Splash Screen**: Installation now shows Allumi logo during setup (not instant anymore)
- **Progress Indication**: Visual feedback during installation process
- **Professional Experience**: Installation feels more polished like Slack/Discord

### Performance & Sync
- **Instant Sync**: Activities are sent immediately to Allumi (no 60-second delay)
- **Real-time Tracking**: Just like "old Windows Sensor 1.0" - smooth and instant
- **Smaller Size**: Framework-dependent build requires .NET 8 Runtime but is much smaller

---

## 🔧 How to Install This Version

### Step 1: Trigger the Build
1. Go to: https://github.com/fini-commits/Allumi.WindowsSensor/actions
2. Click "Build Windows Sensor (Squirrel)" workflow
3. Click "Run workflow" → "Run workflow" button
4. Wait ~2-3 minutes for build to complete

### Step 2: Tell Lovable About New Release
Once the build completes:
- Inform Lovable that v1.0.21+ is available
- Lovable will use the new release URL: `https://github.com/fini-commits/Allumi.WindowsSensor/releases/latest/download/Setup.exe`

### Step 3: One Final Manual Install
1. Go to Allumi dashboard
2. Download the custom installer (with your config embedded)
3. **Uninstall old version** (v1.0.18) first
4. Run the new installer
5. ✨ Done! Auto-updates will handle everything from now on

---

## 📋 What You'll See After Installing

### System Tray Icon
- Right-click the Allumi icon in your system tray
- You should see:
  ```
  Version 1.0.21         (grayed out - just for display)
  ─────────────────
  Open Log Folder
  Live Tail (PowerShell)
  ─────────────────
  Check for Updates     (← NEW!)
  ─────────────────
  Show config path
  ─────────────────
  Quit
  ```

### Hover Over Icon
- Tooltip shows: `Allumi Sensor v1.0.21 • Tracking`

### Update Dialog (when available)
- Automatic check on startup
- Or manual check via "Check for Updates" menu
- Shows: "A new version X.X.X is available! Current version: 1.0.21"

---

## 🐛 Fixes Included

This release includes all previous fixes:
- ✅ Config extraction from Setup.exe (multi-location fallback)
- ✅ Instant sync instead of 60-second batches
- ✅ Smaller app size (framework-dependent)
- ✅ OAuth deep link protocol (allumi://)
- ✅ Windows Credential Manager integration
- ✅ First-run authentication UI

---

## 🚀 What Happens Next

1. **This Install**: ONE final manual download and install
2. **Future Updates**: 
   - App checks for updates automatically
   - You'll see a dialog: "New version available!"
   - Click "Yes" → App updates and restarts
   - **No more manual downloads!**

3. **Auto-Update URL**: Currently set to `https://your-host/updates`
   - This needs to be configured in production
   - Options: GitHub releases, CDN, or custom server
   - Squirrel will fetch updates from this location

---

## 📊 Verify It's Working

After installing v1.0.21:

1. **Check Version**:
   - Right-click tray icon
   - Look for "Version 1.0.21" in menu
   - Hover over icon to see version in tooltip

2. **Check Sync**:
   - Switch between apps (Chrome, Code, PowerShell, etc.)
   - Ask Lovable to check Supabase logs
   - Should see activities coming in INSTANTLY (no 60s delay)

3. **Check Updates**:
   - Right-click → "Check for Updates"
   - Should say "No updates available" (you're on latest!)

---

## 🎯 Current Status

**Version**: 1.0.21 (bumped from 1.0.18)  
**Committed**: ✅ All changes pushed to main branch  
**Build Needed**: ⏳ Need to manually trigger GitHub Actions workflow  
**Auto-Updates**: ⚙️ Placeholder URL needs production configuration  

---

## 💡 Tips

- **Don't worry about the "https://your-host/updates" placeholder** - it won't cause errors, updates just won't work until configured
- **Keep the old installer** - if something goes wrong, you can reinstall v1.0.18
- **Check logs** - Right-click tray icon → "Open Log Folder" or "Live Tail (PowerShell)"
- **Instant sync is aggressive** - if you notice any issues, let me know!

---

## 🔗 Quick Links

- **GitHub Actions**: https://github.com/fini-commits/Allumi.WindowsSensor/actions
- **Latest Release**: https://github.com/fini-commits/Allumi.WindowsSensor/releases/latest
- **Repository**: https://github.com/fini-commits/Allumi.WindowsSensor
