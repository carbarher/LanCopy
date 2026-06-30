# Script de diagnóstico de conectividad de LanCopy

param(
    [string]$RemoteIp,
    [int]$RemotePort = 8742
)

Write-Host "╔════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  LanCopy Connectivity Diagnostics              ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# 1. Local Network Interfaces
Write-Host "1️⃣  Local Network Configuration:" -ForegroundColor Yellow
[System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() |
    Where-Object { $_.OperationalStatus -eq 'Up' -and $_.NetworkInterfaceType -ne 'Loopback' } |
    ForEach-Object {
        Write-Host "   Network: $($_.Name) ($($_.NetworkInterfaceType))"
        $_.GetIPProperties().UnicastAddresses |
            Where-Object { $_.Address.AddressFamily -eq 'InterNetwork' } |
            ForEach-Object { Write-Host "   ✓ IPv4: $($_.Address)" -ForegroundColor Green }
    }

# 2. Check if server is running locally
Write-Host ""
Write-Host "2️⃣  Local Server Status:" -ForegroundColor Yellow
Write-Host "   Checking if LanCopy server is listening on :8742..."
$serverRunning = $false
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect([System.Net.IPAddress]::Loopback, 8742)
    Write-Host "   ✅ Server IS listening on localhost:8742" -ForegroundColor Green
    $serverRunning = $true
    $tcpClient.Close()
} catch {
    Write-Host "   ❌ Server NOT listening on localhost:8742" -ForegroundColor Red
    Write-Host "      → Is LanCopy running? Check taskbar." -ForegroundColor Yellow
}

# 3. Remote connectivity test
if ($RemoteIp) {
    Write-Host ""
    Write-Host "3️⃣  Remote Connection Test to $RemoteIp`:$RemotePort" -ForegroundColor Yellow
    Write-Host ""
    
    # Ping test
    try {
        $ping = New-Object System.Net.NetworkInformation.Ping
        $reply = $ping.Send($RemoteIp, 2000)
        if ($reply.Status -eq 'Success') {
            Write-Host "   ✅ Ping OK ($($reply.RoundtripTime)ms)" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️  Ping failed: $($reply.Status)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "   ⚠️  Ping error: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    # Port connectivity
    Write-Host ""
    Write-Host "   Testing port $RemotePort..."
    try {
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $ar = $tcpClient.BeginConnect($RemoteIp, $RemotePort, $null, $null)
        $wait = $ar.AsyncWaitHandle.WaitOne(3000, $false)
        
        if ($wait -and $tcpClient.Connected) {
            Write-Host "   ✅ Port $RemotePort is open" -ForegroundColor Green
            Write-Host "      Successfully connected to $RemoteIp`:$RemotePort" -ForegroundColor Green
            $tcpClient.Close()
        } else {
            Write-Host "   ❌ Cannot connect to $RemoteIp`:$RemotePort" -ForegroundColor Red
            Write-Host "      → Check if LanCopy is running on that machine" -ForegroundColor Yellow
            Write-Host "      → Check firewall settings on port $RemotePort" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "   ❌ Connection error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Troubleshooting:" -ForegroundColor Yellow
if (-not $serverRunning) {
    Write-Host "  1. Start LanCopy on this machine first"
    Write-Host "  2. Wait for the UI to show the local IP address"
    Write-Host "  3. Share the IP + PIN code with the remote user"
}
if ($RemoteIp) {
    Write-Host "  If connection still fails:"
    Write-Host "  • Verify the IP address is correct"
    Write-Host "  • Check both machines are on the same network"
    Write-Host "  • Verify port $RemotePort is not blocked by firewall"
    Write-Host "  • Try 'ping $RemoteIp' in Command Prompt"
}
