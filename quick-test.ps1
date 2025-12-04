# Quick Test - ZkFingerBridge
# This script runs the Test API in the background, then starts ZkFingerBridge

Write-Host ""
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "  ZkFingerBridge Quick Test" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host ""

# Check if SDK is installed
Write-Host "Checking SDK installation..." -ForegroundColor Yellow
try {
    $regPath = "Registry::HKEY_CLASSES_ROOT\zkemkeeper.ZKEM"
    Get-Item $regPath -ErrorAction Stop | Out-Null
    Write-Host "✓ SDK is installed" -ForegroundColor Green
} catch {
    Write-Host "✗ SDK is NOT installed!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please run the SDK installer first:" -ForegroundColor Yellow
    Write-Host "  cd 'e:\Fingerprint integration\Standalone-SDK\Communication Protocol SDK(32Bit Ver6.2.4.11)'" -ForegroundColor White
    Write-Host "  .\Fix_Install_SDK_64bit.bat" -ForegroundColor White
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "Starting Test API on http://localhost:5000..." -ForegroundColor Yellow

# Start Test API in background
$testApiPath = "e:\Fingerprint integration\TestHrApi"
$testApiJob = Start-Job -ScriptBlock {
    param($path)
    Set-Location $path
    dotnet run
} -ArgumentList $testApiPath

Write-Host "✓ Test API started (Job: $($testApiJob.Id))" -ForegroundColor Green
Write-Host "  Waiting 5 seconds for API to start..." -ForegroundColor Gray

Start-Sleep -Seconds 5

# Test if API is responding
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5000" -UseBasicParsing -TimeoutSec 5
    Write-Host "✓ Test API is responding" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "✗ Test API failed to start!" -ForegroundColor Red
    Stop-Job $testApiJob
    Remove-Job $testApiJob
    exit 1
}

Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "  Starting ZkFingerBridge..." -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host ""
Write-Host "Watch for:" -ForegroundColor Yellow
Write-Host "  - Device discovery (if using auto-discovery)" -ForegroundColor White
Write-Host "  - Connection to device" -ForegroundColor White
Write-Host "  - Logs being retrieved and synced" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C to stop both applications" -ForegroundColor Yellow
Write-Host ""

# Start ZkFingerBridge (foreground)
try {
    Set-Location "e:\Fingerprint integration\ZkFingerBridge"
    dotnet run
} finally {
    # Clean up: stop Test API when ZkFingerBridge stops
    Write-Host ""
    Write-Host "Stopping Test API..." -ForegroundColor Yellow
    Stop-Job $testApiJob -ErrorAction SilentlyContinue
    Remove-Job $testApiJob -ErrorAction SilentlyContinue
    Write-Host "✓ Cleanup complete" -ForegroundColor Green
    Write-Host ""
}
