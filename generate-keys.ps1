# Script to generate installation keys for testing
param(
    [string]$BranchId = "BR001",
    [string]$BranchName = "Cairo Branch"
)

$apiUrl = "http://localhost:5000/api/keys/generate"

Write-Host "Generating installation key for:"
Write-Host "  Branch ID: $BranchId"
Write-Host "  Name:      $BranchName"
Write-Host "----------------------------------------"

try {
    $uri = "$apiUrl?branchId=$BranchId&branchName=$([Uri]::EscapeDataString($BranchName))"
    $response = Invoke-RestMethod -Uri $uri -Method Post -ErrorAction Stop
    
    if ($response.success) {
        Write-Host "✅ Key Generated Successfully!" -ForegroundColor Green
        Write-Host "   KEY: $($response.key)" -ForegroundColor Cyan
        Write-Host "   (This key can be used once to activate the installer)"
    } else {
        Write-Host "❌ Failed to generate key" -ForegroundColor Red
        Write-Host $response
    }
}
catch {
    Write-Host "❌ Error connecting to API. Is TestHrApi running?" -ForegroundColor Red
    Write-Host $_.Exception.Message
}
