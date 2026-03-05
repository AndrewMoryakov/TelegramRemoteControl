#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Обновляет агента TRC: пересобирает, останавливает сервис, публикует, запускает.
.EXAMPLE
    .\update-agent.ps1
#>

$ServiceName = "TRCAgent"
$ProjectPath = Join-Path $PSScriptRoot "hub-agent\src\TelegramRemoteControl.Agent\TelegramRemoteControl.Agent.csproj"
$PublishDir  = Join-Path $PSScriptRoot "publish\agent\win-x64"
$ConfigFile  = Join-Path $PublishDir "appsettings.json"

Write-Host "=== TRC Agent Update ===" -ForegroundColor Cyan

# 1. Backup config
$configBackup = $null
if (Test-Path $ConfigFile) {
    $configBackup = Get-Content $ConfigFile -Raw
    Write-Host "Config backed up." -ForegroundColor Gray
}

# 2. Stop service
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq "Running") {
    Write-Host "Stopping $ServiceName..." -ForegroundColor Yellow
    Stop-Service $ServiceName -Force
    Start-Sleep -Seconds 2
}

# 3. Publish
Write-Host "Publishing..." -ForegroundColor Yellow
dotnet publish $ProjectPath -c Release -r win-x64 --self-contained false -o $PublishDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish FAILED. Aborting." -ForegroundColor Red
    if ($svc) { Start-Service $ServiceName }
    exit 1
}

# 4. Restore config (dotnet publish may overwrite appsettings.json)
if ($configBackup) {
    Set-Content -Path $ConfigFile -Value $configBackup -Encoding UTF8
    Write-Host "Config restored." -ForegroundColor Gray
}

# 5. Start service
if ($svc) {
    Write-Host "Starting $ServiceName..." -ForegroundColor Yellow
    Start-Service $ServiceName
    Start-Sleep -Seconds 2
    $svc.Refresh()
    if ($svc.Status -eq "Running") {
        Write-Host "Agent updated and running." -ForegroundColor Green
    } else {
        Write-Host "Agent failed to start. Check Event Log." -ForegroundColor Red
    }
} else {
    Write-Host "Service $ServiceName not found. Start manually." -ForegroundColor Yellow
}
