param(
    [string]$Command = "interactive",
    [string]$DeviceIds = "",
    [switch]$Help
)

if ($Help) {
    Write-Host @"
Publisher Manager Runner

Parameters:
  -Command    Command to execute (start, stop, status, interactive)
  -DeviceIds  Comma-separated device IDs for targeted operations
  -Help       Show this help message

Examples:
  .\run-publisher-manager.ps1                          # Interactive mode
  .\run-publisher-manager.ps1 -Command start          # Start all publishers
  .\run-publisher-manager.ps1 -Command enable -DeviceIds "DEV001,DEV002"

"@ -ForegroundColor Green
    exit 0
}

$ErrorActionPreference = "Stop"

# Build if needed
if (!(Test-Path "src/MQTTLoadTest.PublisherManager/bin/Release")) {
    Write-Host "Building Publisher Manager..." -ForegroundColor Cyan
    dotnet build src/MQTTLoadTest.PublisherManager -c Release
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

Write-Host "Starting Publisher Manager..." -ForegroundColor Green
Set-Location "src/MQTTLoadTest.PublisherManager"
dotnet run -c Release -- $args