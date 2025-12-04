# ZKTeco Device Finder - Multi-Port Scanner
# Scans for ZKTeco devices on common ports

param(
    [string]$Subnet = "192.168.0",
    [int[]]$Ports = @(4370, 8080, 80, 443, 5005)  # Common ZKTeco ports
)

Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "  ZKTeco Device Scanner - Multi-Port Detection" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host ""
Write-Host "Scanning: $Subnet.0/24" -ForegroundColor Yellow
Write-Host "Ports: $($Ports -join ', ')" -ForegroundColor Yellow
Write-Host ""

$found = @()

foreach ($port in $Ports) {
    Write-Host "Scanning port $port..." -ForegroundColor Cyan
    
    1..254 | ForEach-Object {
        $ip = "$Subnet.$_"
        $tcp = New-Object System.Net.Sockets.TcpClient
        
        try {
            $connect = $tcp.ConnectAsync($ip, $port)
            $connect.Wait(300)  # 300ms timeout
            
            if ($tcp.Connected) {
                $found += [PSCustomObject]@{
                    IP = $ip
                    Port = $port
                }
                Write-Host "  ✓ Found device at $ip`:$port" -ForegroundColor Green
            }
        } 
        catch { } 
        finally { 
            $tcp.Close() 
        }
    }
}

Write-Host ""
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "  Results" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan

if ($found.Count -eq 0) {
    Write-Host ""
    Write-Host "✗ No devices found on any port" -ForegroundColor Red
    Write-Host ""
    Write-Host "Try:" -ForegroundColor Yellow
    Write-Host "  1. Check device is powered on and connected to network" -ForegroundColor White
    Write-Host "  2. Verify subnet: .\find-zk-device-any-port.ps1 -Subnet '192.168.1'" -ForegroundColor White
    Write-Host "  3. Check device menu for actual IP and port" -ForegroundColor White
} else {
    Write-Host ""
    $found | Format-Table IP, Port -AutoSize
    
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Yellow
    
    foreach ($device in $found) {
        Write-Host ""
        Write-Host "Update appsettings.json with:" -ForegroundColor Cyan
        Write-Host "  `"IpAddress`": `"$($device.IP)`"," -ForegroundColor White
        Write-Host "  `"Port`": $($device.Port)," -ForegroundColor White
        
        Write-Host ""
        Write-Host "Or test connection:" -ForegroundColor Cyan
        Write-Host "  Test-Connection -ComputerName $($device.IP) -Count 2" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "=" * 70 -ForegroundColor Cyan
