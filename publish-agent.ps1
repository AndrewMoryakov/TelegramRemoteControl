param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptRoot
try {
    $project = Join-Path $scriptRoot "hub-agent/src/TelegramRemoteControl.Agent/TelegramRemoteControl.Agent.csproj"
    $output = Join-Path $scriptRoot "publish/agent/$Runtime"

    if (-not (Test-Path $project)) {
        throw "Project file not found: $project"
    }

    Write-Host "Publishing agent ($Runtime, $Configuration)..." -ForegroundColor Cyan

    dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
        -o $output `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true

    Write-Host ""
    Write-Host "Готово:" -ForegroundColor Green
    Write-Host "$output" -ForegroundColor Cyan
}
finally {
    Pop-Location
}
