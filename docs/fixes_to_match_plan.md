# Список правок для соответствия плану (до 3.2 включительно)

Дата: 2026-01-30 (обновлено)

Документ фиксирует **несоответствия плану** (docs/implementation_plan.md), **приоритизацию** и **чек‑лист**. Правки упорядочены по важности.

---

## Приоритизация

### P0 — критично (блокирует работу)
1. **Agent pairing flow**: нет обработки `ReceiveToken`, токен не сохраняется, повторные подключения ломаются. ✅ исправлено
2. **Hub CommandsController**: fallback на первый online агент → риск выполнения команды на неверном ПК. ✅ исправлено

### P1 — важно (падает на runtime / ломает 3.2)
3. **CP866 поддержка**: `Encoding.GetEncoding(866)` без регистрации → возможный `NotSupportedException`. ✅ исправлено

### P2 — средняя важность (отклонение от контракта/плана)
4. **ProcessesExecutor**: требуется `Structured + JsonPayload`, сейчас `Text` + buttons. ✅ исправлено
5. **Menu (этап 2.6)**: отсутствует `/menu`, `ReplyWithMenu`, `ReplyWithBack`, авто‑выбор. ✅ исправлено

### P3 — низкая важность (структурные отличия)
6. **ShellExecutor**: по плану один `ShellExecutor`, сейчас два executor’а.

---

## Детальный список правок

### 1) Завершить pairing‑flow в Agent (этап 2.4)
**Симптом:** агент подключается по pairing code, но не обрабатывает `ReceiveToken` и не сохраняет токен.

**Где:**
- `hub-agent/src/TelegramRemoteControl.Agent/AgentService.cs`
- `hub-agent/src/TelegramRemoteControl.Agent/appsettings.json`

**Что сделать:**
- Добавить `ReceiveToken` handler.
- Сохранить токен в `appsettings.json`, очистить `PairingCode`.
- Переподключиться с токеном.
**Статус:** ✅ выполнено (2026-01-30)

---

### 2) Убрать fallback на «первый онлайн» агент (этап 2.3)
**Симптом:** команды могут уйти на неправильный ПК.

**Где:**
- `hub-agent/src/TelegramRemoteControl.Hub/Controllers/CommandsController.cs`

**Что сделать:**
- Если агент не выбран → вернуть ошибку «Выберите ПК».
- Удалить fallback `GetAgentsByOwner(...).FirstOrDefault`.
**Статус:** ✅ выполнено (2026-01-30)

---

### 3) CP866 (ShellHelper) (этап 3.2)
**Симптом:** `Encoding.GetEncoding(866)` может упасть на runtime.

**Где:**
- `hub-agent/src/TelegramRemoteControl.Agent/Execution/ShellHelper.cs`
- `hub-agent/src/TelegramRemoteControl.Agent/TelegramRemoteControl.Agent.csproj`
- `hub-agent/src/TelegramRemoteControl.Agent/Program.cs`

**Что сделать:**
- Добавить NuGet `System.Text.Encoding.CodePages`.
- Зарегистрировать `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);`.
**Статус:** ✅ выполнено (2026-01-30)

---

### 4) ProcessesExecutor → Structured + JsonPayload (этап 3.1)
**Симптом:** сейчас `Text` + inline‑кнопки, хотя план требует JSON.

**Где:**
- `hub-agent/src/TelegramRemoteControl.Agent/Execution/Executors/ProcessesExecutor.cs`

**Что сделать:**
- Возвращать `ResponseType.Structured`.
- Сериализовать список процессов в `JsonPayload`.
- Кнопки строить на стороне BotService (будет в 3.11).
**Статус:** ✅ выполнено (2026-01-30)

---

### 5) Меню (этап 2.6)
**Симптом:** нет `/menu`, `MenuBuilder`, `Categories`, `ReplyWithMenu/Back`.

**Где:**
- `hub-agent/src/TelegramRemoteControl.BotService/Menu/*`
- `hub-agent/src/TelegramRemoteControl.BotService/Commands/CommandContext.cs`
- `hub-agent/src/TelegramRemoteControl.BotService/BotHandler.cs`
- `hub-agent/src/TelegramRemoteControl.BotService/Program.cs`

**Что сделать:**
- Перенести `MenuBuilder` и `Categories` из монолита.
- Добавить `/menu`.
- Добавить `ReplyWithMenu` и `ReplyWithBack`.
- Автовыбор единственного online‑агента.
**Статус:** ✅ выполнено (2026-01-30)

---

### 6) ShellExecutor структура (этап 3.2)
**Симптом:** два executor’а вместо одного.

**Где:**
- `hub-agent/src/TelegramRemoteControl.Agent/Execution/Executors/CmdExecutor.cs`
- `hub-agent/src/TelegramRemoteControl.Agent/Execution/Executors/PowerShellExecutor.cs`

**Что сделать:**
- (Опционально) объединить в один `ShellExecutor`.

---

## Чек‑лист проверки

Примечание: отметки ниже отражают текущее состояние по коду/смоке‑проверке.

### Pairing
- [x] Новый Agent с PairingCode подключается к Hub.
- [x] Hub отправляет `ReceiveToken`.
- [x] Agent сохраняет токен в `appsettings.json`, очищает PairingCode.
- [x] Agent переподключается уже с токеном.

### Выбор устройства
- [x] Без выбора ПК `/status` возвращает ошибку «Выберите ПК».
- [x] После выбора устройства команды идут на выбранный Agent.

### Shell
- [ ] `Cmd` команда возвращает читаемый вывод.
- [ ] `PowerShell` возвращает вывод без крэша.

### Processes
- [x] Ответ на `Processes` имеет `ResponseType.Structured`.
- [x] `JsonPayload` содержит список процессов (pid, name, ram, cpu).

### Меню
- [x] `/menu` показывает главное меню.
- [x] Кнопка выбора ПК отображает текущее устройство.
- [x] Автовыбор при единственном online‑агенте работает.

---

Если нужно — могу добавить автоматизированный smoke‑test скрипт или чек‑лист ручного тестирования по каждому эндпоинту.
