$ErrorActionPreference = "Stop"

if (-not (Test-Path ".env")) {
    if (Test-Path ".env.example") {
        Copy-Item ".env.example" ".env"
        Write-Host "Created .env from .env.example. Please заполните .env и запустите скрипт снова."
        exit 1
    }

    Write-Error "Файл .env не найден. Создайте его перед запуском."
    exit 1
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Error "Docker не найден в PATH."
    exit 1
}

try {
    docker compose version | Out-Null
} catch {
    Write-Error "Docker Compose не доступен. Убедитесь, что установлен compose v2."
    exit 1
}

docker compose up -d --build

$hubPort = "5000"
try {
    $line = Get-Content ".env" | Where-Object { $_ -match '^\s*HUB_PORT\s*=\s*(.+)\s*$' } | Select-Object -First 1
    if ($line -match '^\s*HUB_PORT\s*=\s*(.+)\s*$') {
        $hubPort = $Matches[1].Trim()
    }
} catch {
    # ignore
}

Write-Host "Готово. Hub доступен на http://localhost:$hubPort"
