# Тест‑план (ручной smoke‑test) для этапов 1.1–3.2

Дата: 2026-01-30

Цель: быстро проверить работоспособность Hub + Agent + BotService после правок по этапам 1.1–3.2.

---

## 0) Подготовка окружения

- Убедиться, что Hub, Agent и BotService собираются и запускаются.
- Заполнить `appsettings.json`:
  - Hub: `HubSettings` (ApiKey, DatabasePath и т.д.)
  - BotService: `BotSettings` (Token, HubUrl, HubApiKey, AuthorizedUsers)
  - Agent: `Agent` (HubUrl, PairingCode или AgentToken)

---

## 1) Hub (REST) — базовая проверка

### 1.1 Pairing code
1. POST `Hub /api/pair/generate` с `{ UserId }`.
2. Проверить, что в ответе пришёл `Code` и `ExpiresAt`.

Ожидаемое:
- Код 200
- Code в формате A–Z, 0–9, длина 6

---

### 1.2 Devices API
1. GET `Hub /api/devices?userId=...`.
2. До регистрации агента список пустой.

Ожидаемое:
- Пустой список или 0 устройств

---

## 2) Agent — pairing и регистрация

### 2.1 Подключение по pairing code
1. В `Agent/appsettings.json` указать `PairingCode`.
2. Запустить Agent.
3. В логах Hub увидеть регистрацию агента.

Ожидаемое:
- Hub отправляет `ReceiveToken`
- Agent сохраняет токен и очищает PairingCode

---

### 2.2 Повторный запуск
1. Остановить Agent.
2. Запустить снова.

Ожидаемое:
- Agent подключается по токену
- PairingCode не используется

---

## 3) BotService — базовая проверка команд

### 3.1 /addpc
1. В Telegram отправить `/addpc`.

Ожидаемое:
- Получен код привязки

---

### 3.2 /pc (выбор устройства)
1. В Telegram отправить `/pc`.
2. Выбрать устройство.

Ожидаемое:
- Inline‑кнопки с ПК
- После выбора: подтверждение

---

### 3.3 /status без выбранного ПК
1. Сбросить выбор (очистить в Hub, если нужно).
2. В Telegram отправить `/status`.

Ожидаемое:
- Ошибка «Выберите ПК»

---

### 3.4 /status с выбранным ПК
1. Выбрать ПК через `/pc`.
2. В Telegram отправить `/status`.

Ожидаемое:
- Получен текст со статусом ПК (MachineName, OS, Uptime)

---

## 4) Agent Executors (3.1–3.2)

### 4.1 Info (Processes, Drives, IP, Monitor, Uptime)
1. Отправить команды через BotService (если есть) или напрямую через Hub:
   - `CommandType.Processes`
   - `CommandType.Drives`
   - `CommandType.Ip`
   - `CommandType.Monitor`
   - `CommandType.Uptime`

Ожидаемое:
- Processes → Structured + JsonPayload
- Drives → текст
- IP → текст
- Monitor → текст
- Uptime → текст

---

### 4.2 Shell (Cmd, PowerShell)
1. `CommandType.Cmd` с `Arguments = "dir"`
2. `CommandType.PowerShell` с `Arguments = "Get-Process"`

Ожидаемое:
- Вывод читаемый (без символов ???? или ошибок кодировки)
- Нет `NotSupportedException`

---

## 5) Меню (2.6)

### 5.1 /menu
1. Отправить `/menu`.

Ожидаемое:
- Главное меню отображается
- Сверху кнопка текущего ПК

### 5.2 Автовыбор
1. Оставить 1 онлайн‑агент.
2. Удалить выбранный ПК.

Ожидаемое:
- Автовыбор происходит автоматически

---

## 6) Регрессия

- [ ] Убедиться, что `/status` работает и на сервисе
- [ ] Убедиться, что агент устойчив к перезапускам Hub

---

## Примечания
- Если есть проблемы с кодировкой в Shell — проверить регистрацию `CodePagesEncodingProvider`.
- Если команда уходит не на тот ПК — проверить `UserSessionManager` и отсутствие fallback.

