# Аудит безопасности hub-agent

**Проект:** TelegramRemoteControl Hub Agent
**Расположение:** `C:\projects\TelegramRemoteControl\hub-agent\src`
**Дата:** 2026-01-30

---

## CRITICAL — требует немедленного внимания

### 1. Hub API не проверяет API-ключ

- `HubSettings.ApiKey` определён, но **нигде не валидируется**
- Контроллеры (`CommandsController`, `DevicesController`, `PairController`) не имеют `[Authorize]` и не проверяют заголовок `X-Api-Key`
- Любой с сетевым доступом может выполнить команду на любом агенте

### 2. SignalR хаб без авторизации

- `AgentHub` не имеет атрибута `[Authorize]`
- Любой клиент может подключиться и вызвать `RegisterAgent()`, пытаясь подобрать токен

### 3. Command injection в CmdExecutor и PowerShellExecutor

Пользовательский ввод подставляется напрямую без экранирования:

```csharp
// CmdExecutor.cs
ShellHelper.RunAsync("cmd.exe", $"/c {command.Arguments}", ct);

// PowerShellExecutor.cs
ShellHelper.RunAsync("powershell.exe", $"-NoProfile -Command {command.Arguments}", ct);
```

Агент работает как Windows Service (SYSTEM) — это полный доступ к системе.

### 4. Нет HTTPS/WSS

- `HubUrl: "http://localhost:5000"` — всё по HTTP
- Токены, команды, данные передаются открытым текстом
- MITM-атака перехватит все данные

---

## HIGH — серьёзные проблемы

### 5. Path traversal в файловых операциях

- `FileListExecutor`, `FileDownloadExecutor`, `FilePreviewExecutor` — путь от пользователя используется без валидации
- Можно запросить любой файл: `C:\Windows\System32\config\SAM`, `..\..\..\..\etc`

### 6. Токены агентов не истекают

- `AgentToken` бессрочный, нет механизма отзыва
- Если утёк — доступ навсегда

### 7. Креденшлы в открытом виде в appsettings.json

- Telegram Bot Token, AgentToken, ApiKey — всё plain text в конфигах

---

## MEDIUM — стоит исправить

### 8. Нет rate limiting

- Перебор паринг-кодов (6 символов ≈ 900K вариантов) ничем не ограничен
- Нет лимита на попытки `RegisterAgent()`

### 9. `AuthorizedUsers: []` — доступ для всех

- Если список пуст, бот доступен **любому** Telegram пользователю

### 10. Системная информация в heartbeat

- `MachineName`, `OsVersion`, `UserName` передаются при регистрации — раскрытие информации

---

## Что в порядке

- SQL-запросы параметризованы — SQL injection не грозит
- Опасные команды (Shutdown, Restart) требуют подтверждения через `ConfirmableProxyCommandBase`
- Размер файлов для скачивания ограничен (45 МБ)
- Паринг-коды имеют TTL (10 минут)

---

## Рекомендуемый порядок исправлений

| Приоритет | Что сделать |
|-----------|------------|
| 1 | Добавить middleware валидации `X-Api-Key` на все Hub эндпоинты |
| 2 | Добавить `[Authorize]` или проверку токена на SignalR хаб |
| 3 | Валидировать/экранировать аргументы в Cmd/PowerShell исполнителях |
| 4 | Валидировать пути в файловых операциях (`Path.GetFullPath` + проверка корня) |
| 5 | Включить HTTPS/WSS |
| 6 | Добавить rate limiting на аутентификацию |
| 7 | Реализовать ротацию/отзыв токенов |
