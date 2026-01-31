param(
    [switch]$NoPause
)

$ErrorActionPreference = "Stop"
$logPath = Join-Path (Get-Location) "deploy.log"

function Try-StartTranscript {
    try {
        Start-Transcript -Path $logPath -Append | Out-Null
    } catch {
        # ignore if transcript is not supported
    }
}

function Try-StopTranscript {
    try {
        Stop-Transcript | Out-Null
    } catch {
        # ignore
    }
}

function Pause-IfNeeded {
    if (-not $NoPause) {
        Write-Host ""
        try {
            Write-Host "Нажмите любую клавишу, чтобы закрыть окно..."
            [Console]::ReadKey($true) | Out-Null
        } catch {
            # Non-interactive host: give time to read the output
            Start-Sleep -Seconds 10
        }
    }
}

function Fail {
    param([string]$Message)
    Write-Error $Message
    Pause-IfNeeded
    exit 1
}

function Get-EnvMap {
    param([string]$Path)
    $map = @{}
    Get-Content $Path | ForEach-Object {
        $line = $_
        if ($line -match '^\s*$' -or $line -match '^\s*#') { return }
        if ($line -match '^\s*([^=]+?)\s*=\s*(.*)\s*$') {
            $key = $Matches[1].Trim()
            $value = $Matches[2].Trim()
            $map[$key] = $value
        }
    }
    return $map
}

try {
    Try-StartTranscript

    if (-not (Test-Path ".env")) {
        if (Test-Path ".env.example") {
            Copy-Item ".env.example" ".env"
            Fail "Created .env from .env.example. Заполните .env и запустите скрипт снова."
        }

        Fail "Файл .env не найден. Создайте его перед запуском."
    }

    $envMap = Get-EnvMap ".env"
    $requiredKeys = @("BOT_TOKEN", "ADMIN_USER_ID", "HUB_API_KEY")
    foreach ($key in $requiredKeys) {
        if (-not $envMap.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($envMap[$key])) {
            Write-Warning "$key не задан в .env"
        }
    }

    if ($envMap.ContainsKey("BOT_TOKEN") -and $envMap["BOT_TOKEN"] -eq "your_telegram_bot_token") {
        Write-Warning "BOT_TOKEN выглядит как placeholder. Проверьте .env."
    }

    if ($envMap.ContainsKey("ADMIN_USER_ID") -and $envMap["ADMIN_USER_ID"] -eq "123456789") {
        Write-Warning "ADMIN_USER_ID выглядит как placeholder. Проверьте .env."
    }

    if ($envMap.ContainsKey("HUB_API_KEY") -and $envMap["HUB_API_KEY"] -eq "shared-secret") {
        Write-Warning "HUB_API_KEY выглядит как placeholder. Проверьте .env."
    }

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Fail "Docker не найден в PATH."
    }

    $composeKind = $null
    try {
        docker compose version | Out-Null
        $composeKind = "docker compose"
    } catch {
        if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
            $composeKind = "docker-compose"
        }
    }

    if (-not $composeKind) {
        Fail "Docker Compose не доступен. Убедитесь, что установлен compose v2 или docker-compose."
    }

    Write-Host "Запуск docker compose..."
    if ($composeKind -eq "docker compose") {
        & docker compose up -d --build
    } else {
        & docker-compose up -d --build
    }

    if ($LASTEXITCODE -ne 0) {
        Fail "docker compose завершился с ошибкой (код $LASTEXITCODE)."
    }

    $hubPort = "5000"
    if ($envMap.ContainsKey("HUB_PORT") -and -not [string]::IsNullOrWhiteSpace($envMap["HUB_PORT"])) {
        $hubPort = $envMap["HUB_PORT"]
    }

    Write-Host "Готово. Hub доступен на http://localhost:$hubPort"
    Pause-IfNeeded
} catch {
    Write-Error "Ошибка: $($_.Exception.Message)"
    if ($_.Exception.InnerException) {
        Write-Error "Inner: $($_.Exception.InnerException.Message)"
    }
    Pause-IfNeeded
    exit 1
} finally {
    Try-StopTranscript
    if (Test-Path $logPath) {
        Write-Host "Лог сохранён в: $logPath"
    }
}
