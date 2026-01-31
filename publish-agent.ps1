param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$project = "hub-agent/src/TelegramRemoteControl.Agent/TelegramRemoteControl.Agent.csproj"
$output = "publish/agent/$Runtime"

dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
    -o $output `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

Write-Host ""
Write-Host "Готово:" -ForegroundColor Green
Write-Host "$output" -ForegroundColor Cyan
