# Состояние проекта: монолит vs Hub‑Agent (функциональность)

> Примечание: legacy‑монолит (папка `src/`) удалён из репозитория.  
> Упоминания монолита ниже — исторический контекст.

Дата: 2026-01-30 (обновлено)

Документ фиксирует **текущее функциональное состояние** проекта в двух реализациях:
- монолит (`src/`)
- новая архитектура (`hub-agent/`)

---

## 1) Монолит (src/)

### Что умеет
- Полный Telegram‑бот с меню, категориями и callback‑обработчиками.
- Рабочие команды:
  - `status`, `uptime`, `monitor`
  - `processes` (кнопки kill/info)
  - `services` (start/stop/restart)
  - `windows` (list/min/max/close/screenshot)
  - `screenshot`
  - `files` (файловый менеджер + скачивание)
  - `ip`, `drives`
  - `cmd`, `powershell`
  - `kill`, `lock`, `restart`, `shutdown`, `sleep`, `hibernate`
- Меню:
  - `MenuBuilder` (главное меню, категории, кнопка “Назад/Меню”)
- Callbacks:
  - процессы, сервисы, окна, файловый менеджер

### Ограничения
- Управляет **только одним ПК** (тем, где запущен бот).
- Невозможно масштабировать на несколько устройств с одним токеном Telegram.

---

## 2) Hub‑Agent архитектура (hub-agent/)

### Что уже работает
**Hub:**
- SignalR хаб (`/agent-hub`)
- REST:
  - `POST /api/commands/execute`
  - `POST /api/pair/generate`
  - `GET /api/devices`
  - `POST /api/devices/select`
  - `GET /api/devices/selected`
  - `POST /api/users/seen`
  - `POST /api/users/notify`
  - `GET /api/users/notify`
- SQLite хранит устройства, пользователей, выбор устройства и pairing‑коды

**Agent:**
- Подключение к Hub, heartbeat
- Выполнение команд:
  - Info: Status, Processes, Drives, Ip, Monitor, Uptime
  - Shell: Cmd, PowerShell
  - Screen: Screenshot, WindowsList, WindowAction, WindowScreenshot
  - Control: Kill (процессы), Services, ServiceAction, Lock, Shutdown, Restart, Sleep, Hibernate
  - Files: FileList, FileDownload, FilePreview

**BotService (Telegram):**
- `/status`, `/uptime`, `/monitor`, `/processes`, `/drives`, `/ip`
- `/cmd`, `/powershell`
- `/screenshot`, `/windows`
- `/services`
- `/files`
- `/lock`, `/shutdown`, `/restart`, `/sleep`, `/hibernate`
- `/kill` (по PID)
- `/addpc` (pairing‑код)
- `/pc` (выбор устройства) + callback `pc:select:*`
- `/menu` (главное меню, категории, кнопка “Меню/Назад”)
- авто‑выбор единственного online‑агента
- callbacks: `pc:list`, `menu`, `cat:*`, `proc:*`, `svc:*`, `win:*`, `f:*`, `confirm:*`
- DeviceStatusMonitor (push‑уведомления о подключении/отключении, опционально по конфигу)
- `/notify` (включить/выключить push‑уведомления)
- `/approve`, `/deny` (админ‑команды, если включены в конфиге)
- `/register` (заявка на доступ, если доступ закрыт)

### Ограничения (сейчас)
- **Уведомления статуса** включаются вручную через `/notify on` и хранятся в БД Hub.
- **Авторизация пользователей** хранится в БД Hub (не в `appsettings.json`).
- **Новые пользователи** по умолчанию требуют одобрения (`/approve`) админом.

---

## 3) Итог по пользователю

### Сейчас в Telegram реально доступно
- `/status`, `/uptime`, `/monitor`, `/processes`, `/drives`, `/ip`
- `/cmd`, `/powershell`
- `/screenshot`, `/windows`
- `/services`, `/files`
- `/lock`, `/shutdown`, `/restart`, `/sleep`, `/hibernate`
- `/addpc` (выдать pairing‑код)
- `/pc` (выбор ПК)
- `/menu` (меню и категории)

### Чего пока нет (но есть в монолите)
Нет.

---

## 4) Вывод

Монолит **функционально богат**, но ограничен одним ПК.
Hub‑Agent архитектура **на паритете** по ключевым командам. Остались только опциональные улучшения.

