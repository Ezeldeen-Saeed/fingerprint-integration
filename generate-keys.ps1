# Generate Installation Keys for Testing
# Run this after starting TestHrApi

$apiUrl = "http://localhost:5000"

Write-Host "=== Installation Key Generator ===" -ForegroundColor Cyan
Write-Host ""

# Define test branches
$branches = @(
    @{ Id = "BR001"; Name = "Cairo Main Office" },
    @{ Id = "BR002"; Name = "Alexandria Branch" },
    @{ Id = "BR003"; Name = "Giza Branch" }
)

Write-Host "Generating installation keys..." -ForegroundColor Yellow
Write-Host ""

foreach ($branch in $branches) {
    $url = "$apiUrl/api/keys/generate?branchId=$($branch.Id)&branchName=$([uri]::EscapeDataString($branch.Name))"
    
    try {
        $response = Invoke-RestMethod -Uri $url -Method Post -ContentType "application/json"
        
        if ($response.Success) {
            Write-Host "✅ $($branch.Name)" -ForegroundColor Green
            Write-Host "   Key: $($response.Key)" -ForegroundColor White
            Write-Host ""
        }
    }
    catch {
        Write-Host "❌ Failed to generate key for $($branch.Name)" -ForegroundColor Red
        Write-Host "   Error: $_" -ForegroundColor Red
        Write-Host ""
    }
}

Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "To view all keys, visit:" -ForegroundColor Yellow
Write-Host "http://localhost:5000/api/keys" -ForegroundColor White
Write-Host ""
Write-Host "To test activation:" -ForegroundColor Yellow
Write-Host 'Invoke-RestMethod -Uri "http://localhost:5000/api/installer/activate" -Method Post -Body (ConvertTo-Json @{installationKey="YOUR-KEY-HERE"}) -ContentType "application/json"' -ForegroundColor White
