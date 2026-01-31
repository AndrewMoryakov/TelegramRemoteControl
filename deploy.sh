#!/usr/bin/env bash
set -euo pipefail

if [ ! -f ".env" ]; then
  if [ -f ".env.example" ]; then
    cp .env.example .env
    echo "Created .env from .env.example. Please заполните .env и запустите скрипт снова."
    exit 1
  fi
  echo "Файл .env не найден. Создайте его перед запуском."
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker не найден в PATH."
  exit 1
fi

if ! docker compose version >/dev/null 2>&1; then
  echo "Docker Compose не доступен. Убедитесь, что установлен compose v2."
  exit 1
fi

docker compose up -d --build

hub_port="5000"
if grep -qE '^\s*HUB_PORT\s*=' .env; then
  hub_port="$(grep -E '^\s*HUB_PORT\s*=' .env | head -n1 | sed -E 's/^\s*HUB_PORT\s*=\s*//')"
fi

echo "Готово. Hub доступен на http://localhost:${hub_port}"
