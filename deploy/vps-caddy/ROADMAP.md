# Roadmap (VPS + Caddy)

## Этап 1 — Базовый VPS
- домен + A‑запись
- Docker + compose
- Caddy с HTTPS
- Hub + BotService

## Этап 2 — Надёжность
- резервные копии `hub.db`
- лимиты/рейт‑лимиты
- audit‑лог команд

## Этап 3 — Масштаб
- отдельная БД (PostgreSQL)
- multiple BotService instances
- rate‑limit per user

## Этап 4 — UX
- web‑панель пользователя (опционально)
- self‑service управление устройствами
