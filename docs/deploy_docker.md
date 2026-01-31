# Docker deploy (Hub + BotService)

Этот способ подходит для быстрого развёртывания серверной части на новом сервере.

---

## 1) Требования

- Docker + Docker Compose v2

---

## 2) Подготовка

Скопируйте `.env.example` в `.env` и заполните значения:

```
BOT_TOKEN=ваш_telegram_bot_token
ADMIN_USER_ID=ваш_telegram_user_id
HUB_API_KEY=shared-secret
HUB_PORT=5000
```

`ADMIN_USER_ID` — это администратор, который может делать `/approve` и `/deny`.

---

## 3) Запуск

В корне репозитория:

```
docker compose up -d --build
```

Hub будет доступен на `http://<server>:HUB_PORT` (по умолчанию `5000`).

---

## 4) Несколько сервисов на одном сервере

Если на сервере уже есть сервис, который использует порт 5000:
```
HUB_PORT=5001
```

Внутри Docker контейнера Hub всё равно работает на `5000`, меняется только внешний порт.

---

## 5) Где хранится база

SQLite хранится в volume `hub-data`:

```
docker volume ls
docker volume inspect hub-data
```

Если нужен бэкап:

```
docker run --rm -v hub-data:/data -v ${PWD}:/backup alpine cp /data/hub.db /backup/hub.db
```

---

## 6) Остановка

```
docker compose down
```

---

Если нужна настройка через обратный прокси (Nginx/Caddy) — скажи, добавлю пример.

---

## VPS + Caddy (HTTPS)

Если нужен публичный доступ через домен и HTTPS — используй пакет:

`deploy/vps-caddy/GETTING_STARTED.md`
