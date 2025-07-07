param(
    [string]$Command = "interactive",
    [string]$DeviceIds = "",
    [switch]$Help
)

if ($Help) {
    Write-Host @"
Subscriber Manager Runner

Parameters:
  -Command    Command to execute (start, stop, status, interactive)
  -DeviceIds  Comma-separated device IDs for targeted operations
  -Help       Show this help message

Examples:
  .\run-subscriber-manager.ps1                         # Interactive mode
  .\run-subscriber-manager.ps1 -Command start         # Start subscriber
  .\run-subscriber-manager.ps1 -Command subscribe -DeviceIds "DEV001,DEV002"

"@ -ForegroundColor Green
    exit 0
}

$ErrorActionPreference = "Stop"

# Build if needed
if (!(Test-Path "src/MQTTLoadTest.SubscriberManager/bin/Release")) {
    Write-Host "Building Subscriber Manager..." -ForegroundColor Cyan
    dotnet build src/MQTTLoadTest.SubscriberManager -c Release
}

# Prepare arguments
$args = @()
if ($Command -ne "interactive") {
    $args += $Command
    if ($DeviceIds) {
        $args += "--device-ids"
        $args += $DeviceIds
    }
}

Write-Host "Starting Subscriber Manager..." -ForegroundColor Green
Set-Location "src/MQTTLoadTest.SubscriberManager"
dotnet run -c Release -- $args