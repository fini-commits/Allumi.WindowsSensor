# Test Script for Smart Categorization
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  Allumi v1.0.21 Testing Guide" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Check if app is installed
$appPath = "$env:LOCALAPPDATA\AllumiWindowsSensor"
if (Test-Path $appPath) {
    Write-Host "✅ App installed at: $appPath" -ForegroundColor Green
    
    # Check version
    $versions = Get-ChildItem $appPath -Directory | Where-Object {$_.Name -like "app-*"}
    Write-Host "📦 Installed versions:" -ForegroundColor Yellow
    $versions | ForEach-Object { Write-Host "   - $($_.Name)" }
} else {
    Write-Host "❌ App not installed yet" -ForegroundColor Red
}

Write-Host ""

# Check if app is running
$process = Get-Process | Where-Object {$_.ProcessName -like "*Allumi*"}
if ($process) {
    Write-Host "✅ App is running (PID: $($process.Id))" -ForegroundColor Green
} else {
    Write-Host "⚠️  App is not running" -ForegroundColor Yellow
}

Write-Host ""

# Check log file
$logPath = "$env:APPDATA\Allumi\sensor.log"
if (Test-Path $logPath) {
    Write-Host "✅ Log file exists at: $logPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "📊 Recent activities (last 10):" -ForegroundColor Cyan
    Write-Host "================================" -ForegroundColor Cyan
    Get-Content $logPath -Tail 10 | ForEach-Object {
        Write-Host $_ -ForegroundColor White
    }
    
    Write-Host ""
    Write-Host "🔍 Checking for smart categories..." -ForegroundColor Cyan
    $withConfidence = Select-String -Path $logPath -Pattern "confidence=" -SimpleMatch | Select-Object -Last 5
    if ($withConfidence) {
        Write-Host "✅ Smart categorization is WORKING!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Recent categorized activities:" -ForegroundColor Yellow
        $withConfidence | ForEach-Object {
            Write-Host "   $($_.Line)" -ForegroundColor White
        }
    } else {
        Write-Host "⚠️  No activities with confidence scores found yet" -ForegroundColor Yellow
        Write-Host "   Try switching between apps for 1+ minute each" -ForegroundColor Gray
    }
} else {
    Write-Host "❌ Log file not found yet" -ForegroundColor Red
    Write-Host "   App might not have started tracking yet" -ForegroundColor Gray
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  Testing Instructions" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "1. Open VS Code → Use for 1+ minute → Switch away" -ForegroundColor White
Write-Host "2. Open Chrome → YouTube 'Python Tutorial' → Watch 1+ minute → Switch away" -ForegroundColor White
Write-Host "3. Open Chrome → YouTube 'Music Video' → Watch 1+ minute → Switch away" -ForegroundColor White
Write-Host "4. Run this script again to see categorized activities!" -ForegroundColor White
Write-Host ""
Write-Host "Expected Results:" -ForegroundColor Yellow
Write-Host "  - VS Code → Development (90% confidence)" -ForegroundColor Gray
Write-Host "  - YouTube Tutorial → Learning (85% confidence)" -ForegroundColor Gray
Write-Host "  - YouTube Music → Entertainment (85% confidence)" -ForegroundColor Gray
Write-Host ""
Write-Host "To watch live: Get-Content '$logPath' -Wait -Tail 5" -ForegroundColor Cyan
Write-Host ""
