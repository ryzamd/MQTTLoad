param(
    [string]$Configuration = "Release",
    [switch]$Clean,
    [switch]$Help
)

if ($Help) {
    Write-Host @"
Build All Projects

Parameters:
  -Configuration  Build configuration (Debug/Release, default: Release)
  -Clean          Clean before build
  -Help           Show this help message

"@ -ForegroundColor Green
    exit 0
}

$ErrorActionPreference = "Stop"

if ($Clean) {
    Write-Host "Cleaning solution..." -ForegroundColor Cyan
    dotnet clean -c $Configuration
}

Write-Host "Building MQTT Load Test Solution..." -ForegroundColor Blue
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow

# Build solution
dotnet build -c $Configuration

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✓ Build successful!" -ForegroundColor Green
    Write-Host "`nBuilt applications:" -ForegroundColor Yellow
    Write-Host "  Publisher Manager: src/MQTTLoadTest.PublisherManager/bin/$Configuration/net8.0/" -ForegroundColor White
    Write-Host "  Subscriber Manager: src/MQTTLoadTest.SubscriberManager/bin/$Configuration/net8.0/" -ForegroundColor White
    
    Write-Host "`nRun applications:" -ForegroundColor Yellow
    Write-Host "  .\deployment\run-publisher-manager.ps1" -ForegroundColor White
    Write-Host "  .\deployment\run-subscriber-manager.ps1" -ForegroundColor White
} else {
    Write-Host "`n✗ Build failed!" -ForegroundColor Red
    exit 1
}