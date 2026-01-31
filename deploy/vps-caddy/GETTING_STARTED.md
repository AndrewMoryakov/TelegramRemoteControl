# VPS + Caddy (HTTPS) — Быстрый старт

Этот набор файлов нужен, чтобы развернуть Hub+BotService на VPS с HTTPS.

---

## 1) Что нужно

- VPS с публичным IP
- Домен (A‑запись на IP VPS)
- Docker + Docker Compose v2

---

## 2) Подготовка

Перейдите в `deploy/vps-caddy` и создайте `.env`:

```
cp .env.example .env
```

Заполните:
```
BOT_TOKEN=...
ADMIN_USER_ID=...
HUB_API_KEY=...
CADDY_EMAIL=you@example.com
HUB_DOMAIN=hub.example.com
```

`HUB_API_KEY` — общий секрет для BotService ↔ Hub (пока **не валидируется** на стороне Hub).

---

## 3) Запуск

Из папки `deploy/vps-caddy`:

```
docker compose up -d --build
```

Caddy сам выпустит HTTPS‑сертификат и поднимет домен `https://hub.example.com`.

---

## 4) Проверка

- Открой `https://hub.example.com/` → должен быть ответ `TelegramRemoteControl Hub`
- В боте: `/start`

---

## 5) Где хранится база

SQLite хранится в volume `hub-data`.  
Резервное копирование:

```
docker run --rm -v hub-data:/data -v ${PWD}:/backup alpine cp /data/hub.db /backup/hub.db
```

---

## 6) Обновление

```
docker compose pull
docker compose up -d --build
```

---

Если нужен отдельный домен под BotService — можно добавить второй сайт в Caddyfile.
