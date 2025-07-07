param(
    [string]$BrokerHost = "192.168.6.50",
    [switch]$SkipBrokerTest,
    [switch]$Help
)

if ($Help) {
    Write-Host @"
MQTT Load Test Windows Setup Script

Parameters:
  -BrokerHost     MQTT broker hostname (default: 192.168.6.50)
  -SkipBrokerTest Skip broker connectivity test
  -Help           Show this help message

Examples:
  .\setup-windows.ps1
  .\setup-windows.ps1 -BrokerHost "192.168.1.100"
  .\setup-windows.ps1 -SkipBrokerTest

"@ -ForegroundColor Green
    exit 0
}

$ErrorActionPreference = "Stop"

Write-Host "=== MQTT Load Test Environment Setup ===" -ForegroundColor Blue
Write-Host "Broker Host: $BrokerHost" -ForegroundColor Yellow

# Check .NET 8.0 SDK
Write-Host "`nChecking .NET 8.0 SDK..." -ForegroundColor Cyan
try {
    $dotnetVersion = dotnet --version
    if ($dotnetVersion -lt "8.0") {
        throw "Requires .NET 8.0 or higher. Current: $dotnetVersion"
    }
    Write-Host "✓ .NET SDK $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET 8.0 SDK not found" -ForegroundColor Red
    Write-Host "Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Create directory structure
Write-Host "`nCreating project structure..." -ForegroundColor Cyan
$directories = @(
    "src/MQTTLoadTest.Core/Models",
    "src/MQTTLoadTest.Core/Services", 
    "src/MQTTLoadTest.Core/Interfaces",
    "src/MQTTLoadTest.Core/Utils",
    "src/MQTTLoadTest.PublisherManager/Services",
    "src/MQTTLoadTest.SubscriberManager/Services",
    "config",
    "logs",
    "deployment"
)

foreach ($dir in $directories) {
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "✓ Created $dir" -ForegroundColor Green
    }
}

# Update configuration with broker host
Write-Host "`nUpdating configuration..." -ForegroundColor Cyan
$configPath = "config/mqtt-config.json"
if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    $config.MqttConfiguration.BrokerHost = $BrokerHost
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath
    Write-Host "✓ Updated broker host to $BrokerHost" -ForegroundColor Green
}

# Test broker connectivity
if (!$SkipBrokerTest) {
    Write-Host "`nTesting broker connectivity..." -ForegroundColor Cyan
    try {
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $tcpClient.ConnectAsync($BrokerHost, 1883).Wait(5000)
        if ($tcpClient.Connected) {
            Write-Host "✓ Broker connectivity test passed" -ForegroundColor Green
            $tcpClient.Close()
        } else {
            Write-Host "⚠ Cannot connect to broker at ${BrokerHost}:1883" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "⚠ Broker connectivity test failed: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host "`n=== Setup Complete ===" -ForegroundColor Blue
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Build solution: dotnet build" -ForegroundColor White
Write-Host "2. Run Publisher Manager: .\run-publisher-manager.ps1" -ForegroundColor White
Write-Host "3. Run Subscriber Manager: .\run-subscriber-manager.ps1" -ForegroundColor White