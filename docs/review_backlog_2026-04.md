# Backlog по итогам code review (апрель 2026)

Дата ревью: 2026-04-22
Автор: deep review веткой `master` @ b8c1424

Документ фиксирует проблемы, найденные при разборе текущего состояния Hub-Agent (`hub-agent/src/`). Задачи сгруппированы по приоритету и снабжены:
- **Симптом** — как проявляется у пользователя / в рантайме;
- **Корень** — что именно в коде неверно (файл:строка);
- **Фикс** — минимальный план исправления;
- **Тест** — как подтвердить, что починено.

Сводная таблица приоритетов — в конце документа.

---

## P0 — Критические (корректность, безопасность)

### BL-01. `EnsureAuthorizedAsync` — fail-open при любом исключении ✅ исправлено

**Файл:** `hub-agent/src/TelegramRemoteControl.BotService/BotHandler.cs:585-589`

**Симптом:** если Hub недоступен или `ReportUserSeen` бросает исключение — `catch` возвращает `true`, и **любой** пользователь Telegram проходит авторизацию. Это нивелирует всю модель доступа.

**Корень:** комментарий в строке 583 заявляет «fail-closed», но `catch` противоречит ему:
```csharp
catch (Exception ex)
{
    _logger.LogDebug(ex, "Failed to report user seen");
    return true;   // ← должен быть false
}
```

**Фикс:** вернуть `false`. Логировать на уровне Warning, а не Debug. Уведомить админов (если доступны) о том, что Hub недоступен.

**Тест:** остановить Hub, отправить сообщение от неадминского аккаунта — должно прийти «⛔ Нет доступа», **а не** выполниться команда.

---

### BL-02. Pairing-код хранится plain-text, TTL 180 дней, нет rate-limit ✅ исправлено

**Файлы:**
- `hub-agent/src/TelegramRemoteControl.Hub/Controllers/PairController.cs`
- `hub-agent/src/TelegramRemoteControl.Hub/Data/HubDbContext.cs:386-401` (`AddPairingRequest`)
- `hub-agent/src/TelegramRemoteControl.Hub/HubSettings.cs:12` (`PairingCodeTtlDays = 180`)
- `hub-agent/src/TelegramRemoteControl.Hub/Hubs/AgentHub.cs:70-106` (`RegisterAgent`)

**Симптом:** 6-символьный код из 32-символьного алфавита → ~10⁹ вариантов. При 180-дневном TTL и отсутствии rate-limit на `RegisterAgent` (SignalR) код можно перебрать. Plain-text хранение = утечка при доступе к БД/логам.

**Корень:**
1. БД хранит `Code` as-is в `PairingRequests`.
2. `AgentHub.RegisterAgent` вызывает `_db.GetPairingRequest(credential)` по прямому ключу — сравнение открытого кода.
3. SignalR-подключения не попадают под `UseRateLimiter` (он навешен только на `/api/*` в `Program.cs:48-53`).
4. Дефолт `PairingCodeTtlDays = 180` — мотивировано отсутствием push-механизма, но избыточно.

**Фикс:**
1. Снизить дефолт TTL до 15 минут.
2. Хранить `SHA256(code)` в БД; на вход `RegisterAgent` — хешировать и сравнивать.
3. Считать неудачные `RegisterAgent` попытки в `ConcurrentDictionary<IP, (count, firstAt)>`; при 5+ за минуту — `Context.Abort()` + лог.
4. Возможно, требовать `X-Hub-Key` и для SignalR (уже есть), но дополнительно ограничить «окно pairing» — например, владелец должен явно открыть его через `/addpc`.

**Тест:** (1) сгенерировать код, через 16 минут попытаться зарегистрироваться — отказ; (2) скриптом стукаться в SignalR с невалидными кодами — после 5+ попыток ConnectionId закрыт.

---

### BL-03. `PathValidator` — prefix collision + дефолт пускает любой путь ✅ исправлено

**Файл:** `hub-agent/src/TelegramRemoteControl.Agent/Helpers/PathValidator.cs:39`

**Симптом:** при `FileRootPath = "C:\Users\Foo"` путь `C:\Users\FooBar\secret.txt` проходит проверку. При дефолтной пустой `FileRootPath` — пускается **любой** путь, включая `C:\Windows\System32\config\SAM`.

**Корень:**
```csharp
if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
    return null;
```
`StartsWith` без разделителя даёт false-positive на одноимённых префиксах. Плюс при `rootPath == null` блок сравнения пропускается — `Normalize` возвращает любой валидный путь.

**Фикс:**
1. Нормализовать `normalizedRoot` до окончания на `Path.DirectorySeparatorChar`:
   ```csharp
   var rootWithSep = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
   if (!fullPath.Equals(normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
       && !fullPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
       return null;
   ```
   Либо `Path.GetRelativePath(root, full)` + проверка отсутствия `..`.
2. Решить с дефолтом:
   - **Строго:** при `FileRootPath = ""` — отказываться открывать любые пути (fail-safe).
   - **Мягко:** оставить текущее поведение, но в `Program.cs` агента логировать `LogWarning` при пустом `FileRootPath`.

**Тест:**
1. `FileRootPath = "C:\Users\Foo"`, запросить `C:\Users\FooBar\x.txt` → отказ.
2. Пустой `FileRootPath`, предложенный режим «strict» — `/files C:\Windows\System32\config` возвращает ошибку.

---

### BL-04. Command injection в `CmdExecutor` / `PowerShellExecutor` ✅ частично исправлено

**Заметка по частичному фиксу (апрель 2026):**
- `PowerShellExecutor` уже использовал `-EncodedCommand` — от shell-инъекции защищён.
- `CmdExecutor` остаётся как есть (free-form cmd-режим — это фича).
- Добавлены два гейта в BotService:
  1. `BotSettings.ShellAllowedUsers` — явный allowlist. Если пусто — доступ только у `AuthorizedUsers`.
  2. `BotSettings.ShellMaxArgumentLength` (default 2000) — лимит длины аргумента.
- Gate применён в `CmdCommand`, `PowerShellCommand`, `BotHandler.HandleShellMessageAsync`.

Не сделано (выносится в отдельный тикет):
- Запуск shell не под SYSTEM, а в пользовательской сессии.
- `ConfirmableProxyCommandBase` для shell — конфликтует с интерактивным shell-режимом по UX.
- Расширенный audit (полный аргумент без truncation — см. BL-20).

---

**Файлы:**
- `hub-agent/src/TelegramRemoteControl.Agent/Execution/Executors/CmdExecutor.cs:20`
- `hub-agent/src/TelegramRemoteControl.Agent/Execution/Executors/PowerShellExecutor.cs` (аналогично)

**Симптом:** агент обычно запущен как Windows Service (SYSTEM). Пользовательский ввод пробрасывается в shell без экранирования:
```csharp
ShellHelper.RunAsync("cmd.exe", $"/c {command.Arguments}", ct);
```
Telegram-пользователь может выполнить любую команду под SYSTEM. Доверительная граница держится только на Telegram-авторизации; BL-01 её ломает.

**Корень:** дизайн `/cmd` и `/powershell` подразумевает free-form shell, но:
- агент не запрашивает подтверждения;
- нет белого списка владельцев (всё делят `AuthorizedUsers`);
- нет логирования вывода shell-команд отдельно от audit (audit пишет только CommandType/Arguments, обрезается до 500 символов).

**Фикс:**
1. **Защита в глубину:** завести отдельный флаг `BotSettings.ShellAllowedUsers: long[]` — shell-команды только им.
2. Всегда требовать подтверждения (`ConfirmableProxyCommandBase`) для `/cmd` / `/powershell`.
3. Ограничить аргументы по длине (напр. 2000 символов), чтобы исключить многострочные пейлоады.
4. Для PowerShell использовать `-EncodedCommand` (Base64), это безопаснее для одного лайнера и убирает интерпретацию спецсимволов shell.
5. Рассмотреть запуск shell-команд не под SYSTEM, а под интерактивной сессией пользователя (`SessionInterop.RunInUserSession` уже есть для скриншотов).

**Тест:** добавить интеграционный тест — аргумент `foo & calc.exe` с ожиданием, что `calc.exe` **не** запустится без подтверждения / при неразрешённом пользователе.

---

### BL-05. `ConfirmCallbackHandler` — double-execute деструктивных команд

**Файл:** `hub-agent/src/TelegramRemoteControl.BotService/Callbacks/Impl/ConfirmCallbackHandler.cs:48-67`

**Симптом:** пользователь дважды тапает «✅ Да» (или Telegram ретранслирует callback при нестабильной сети) → команда выполняется дважды. Для `/shutdown`, `/restart`, `/kill`, `/sleep` это реальная проблема.

**Корень:** порядок операций:
```csharp
await confirmable.ExecuteConfirmedAsync(commandCtx);   // 1. блокирует колбэк
await ctx.Bot.DeleteMessage(...);                       // 2. только после выполнения убираем кнопку
await ctx.Bot.AnswerCallbackQuery(...);                 // 3.
```
Кнопка остаётся кликабельной на всё время выполнения. `/shutdown` на агенте — это >1 секунды, пользователь успевает тапнуть повторно.

**Фикс:**
1. Первыми действиями:
   ```csharp
   await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, "Выполняю...", ct);
   await ctx.Bot.EditMessageReplyMarkup(ctx.ChatId, ctx.MessageId!.Value, replyMarkup: null, ct);
   ```
2. Затем `ExecuteConfirmedAsync`.
3. По завершению — при желании `DeleteMessage`.

Альтернативно: `ConcurrentDictionary<(long userId, string actionId), SemaphoreSlim>` для идемпотентности.

**Тест:** ручной клик ×2 за 100 мс по подтверждению shutdown в dev-боте; в audit-log должна быть одна запись.

---

### BL-06. Zombie-agent после рестарта Hub — `Heartbeat` молча игнорируется

**Файл:** `hub-agent/src/TelegramRemoteControl.Hub/Hubs/AgentHub.cs:121-130`

**Симптом:** после рестарта Hub бывает, что SignalR-коннект не рвётся (зависит от keepalive timing). Следующий `Heartbeat` агента приходит на новый Hub, где `AgentManager._agentById` пуст. Код:
```csharp
var agentId = _agentManager.GetAgentIdByConnection(Context.ConnectionId);
if (agentId != null)
    _agentManager.UpdateHeartbeat(agentId, info);
// если null — ничего, return Task.CompletedTask
```
Агент считает себя живым, Hub — считает, что агента нет. Все команды пользователя получают `Выберите ПК: /pc`.

**Корень:** `Heartbeat` не умеет запрашивать переподключение/перерегистрацию. `AgentService` ориентируется только на событие `_connection.Closed` / `Reconnected`, которые не срабатывают в этом сценарии.

**Фикс:** в `Heartbeat`:
```csharp
public Task Heartbeat(AgentInfo info)
{
    var agentId = _agentManager.GetAgentIdByConnection(Context.ConnectionId);
    if (agentId == null)
    {
        _logger.LogWarning("Heartbeat from unknown connection {ConnId}, aborting to force re-register", Context.ConnectionId);
        Context.Abort();
        return Task.CompletedTask;
    }
    _agentManager.UpdateHeartbeat(agentId, info);
    _ = _db.UpdateAgentLastSeenAsync(agentId);
    return Task.CompletedTask;
}
```
`Context.Abort` заставит агента переподключиться через `_connection.Reconnected` → `RegisterAgent`.

**Тест:** (1) поднять агента, дождаться heartbeat; (2) `docker compose restart hub`; (3) через 30–60 сек агент должен быть `online` без ручного вмешательства.

---

### BL-07. Нет liveness-таймаута — команды висят 120 с на мёртвом агенте

**Файлы:**
- `hub-agent/src/TelegramRemoteControl.Hub/Services/AgentManager.cs`
- `hub-agent/src/TelegramRemoteControl.Hub/HubSettings.cs:10` (`AgentTimeoutSeconds = 90`, нигде не используется)
- `hub-agent/src/TelegramRemoteControl.Hub/Controllers/CommandsController.cs:100`

**Симптом:** у агента жёстко упало железо / сеть провалилась без FIN → SignalR `OnDisconnectedAsync` не срабатывает сразу. `AgentManager.IsOnline = true`, `CommandsController` шлёт команду → `PendingCommandStore.WaitForResponse` висит 120 с (300 с для AiChat) до таймаута. Пользователь сидит и ждёт.

**Корень:** `AgentTimeoutSeconds` — dead code. Нет фонового service'а, который бы помечал протухших агентов.

**Фикс:**
1. Добавить `AgentLivenessMonitor : BackgroundService` в Hub:
   ```csharp
   while (!ct.IsCancellationRequested)
   {
       var threshold = TimeSpan.FromSeconds(_settings.AgentTimeoutSeconds);
       foreach (var agent in _agentManager.GetAllAgents().Where(a => a.IsOnline))
       {
           if (DateTime.UtcNow - agent.LastHeartbeat > threshold)
           {
               _logger.LogWarning("Agent {AgentId} timed out, marking offline", agent.AgentId);
               _agentManager.SetDisconnected(agent.ConnectionId);
           }
       }
       await Task.Delay(TimeSpan.FromSeconds(30), ct);
   }
   ```
2. В `AddSignalR` настроить:
   ```csharp
   options.KeepAliveInterval = TimeSpan.FromSeconds(15);
   options.ClientTimeoutInterval = TimeSpan.FromSeconds(AgentTimeoutSeconds);
   ```
3. В `CommandsController.Execute` — после `IsOnline` проверки учесть свежесть heartbeat: если `LastHeartbeat > 60s` — отвечать «🔴 Агент не отвечает» сразу.

**Тест:** `kill -9` агента; в течение `AgentTimeoutSeconds` команда должна вернуть ошибку **без** полного таймаута 120 с.

---

### BL-08. Нет retry на первичный `RegisterAgent` failure

**Файл:** `hub-agent/src/TelegramRemoteControl.Agent/AgentService.cs:91` и heartbeat-loop

**Симптом:** если `RegisterAgent` упал (500 от Hub, сеть мигнула), агент:
- считает себя подключённым (SignalR-коннект живой);
- шлёт heartbeats → Hub их игнорит (BL-06);
- **никогда** не пытается зарегистрироваться повторно, пока SignalR-коннект сам не оборвётся.

**Корень:**
```csharp
// Connect loop — есть retry
while (...) { try { await _connection.StartAsync(...); break; } catch { await Task.Delay(5000); } }
await RegisterAgent(credential, ct);   // один раз, catch логирует и дальше

// Heartbeat loop — не знает, зарегистрирован ли агент
while (...) {
    if (_connection.State == HubConnectionState.Connected)
        await _connection.InvokeAsync("Heartbeat", ...);
}
```

**Фикс:**
1. Ввести флаг `_isRegistered` (volatile bool), выставлять в true **только** после успешного `RegisterAgent`.
2. В heartbeat-loop: если connected, но `!_isRegistered` — снова вызвать `RegisterAgent(...)`.
3. После `Context.Abort()` от Hub (см. BL-06) — `_connection.Reconnected` выставит `_isRegistered = false` → цикл повторит регистрацию.

**Тест:** искусственно вернуть 500 из `RegisterAgent` в хабе (dev-флаг); агент должен ретраить каждые N секунд, в логах — `Registered` после того, как хаб «починили».

---

### BL-09. Race: heartbeat против reconnect после pairing

**Файл:** `hub-agent/src/TelegramRemoteControl.Agent/AgentService.cs:125-186` (`HandleReceiveTokenAsync` / `ReconnectWithTokenAsync`)

**Симптом:** при первом pairing Hub присылает `ReceiveToken` → агент вызывает `_connection.StopAsync` + `StartAsync` + `RegisterAgent(token)`. В этот момент основной heartbeat-loop в `ExecuteAsync` параллельно пытается `InvokeAsync("Heartbeat", ...)`. Возможные исходы:
1. `InvalidOperationException: connection is not active` — лог warning, следующий heartbeat ок.
2. Heartbeat попадает в **новый** коннект **до** `RegisterAgent` → см. BL-06 → zombie.

**Корень:** нет координации между pairing-reconnect и heartbeat-loop. `_reconnectLock` защищает только сам reconnect от параллельного вызова, но не останавливает heartbeat.

**Фикс:** ввести `ManualResetEventSlim _readyForHeartbeat = new(true)`; в `ReconnectWithTokenAsync` — `Reset()` до завершения, `Set()` после `RegisterAgent`. Heartbeat-loop ждёт его перед `InvokeAsync`.

**Тест:** unit/integration — эмулировать pairing поток, в течение первых 30 с после pairing на хабе должен быть ровно один `RegisterAgent` и ни одного «unknown connection heartbeat».

---

## P1 — High: стабильность и целостность данных

### BL-10. TCS повисает на 120 с при исключении `Clients.Client(...).ExecuteCommand`

**Файлы:**
- `hub-agent/src/TelegramRemoteControl.Hub/Controllers/CommandsController.cs:97-101`
- `hub-agent/src/TelegramRemoteControl.Hub/Controllers/BroadcastController.cs:69-71`

**Симптом:** если между `_agentManager.GetAgent(...)` и `Clients.Client(connId).ExecuteCommand(...)` агент отвалился, `ExecuteCommand` может либо бросить исключение (network), либо тихо проглотить вызов (invalid ConnectionId). При исключении `responseTask` не снимается с ожидания — TCS висит в `PendingCommandStore` до таймаута.

**Фикс:**
```csharp
var responseTask = _pendingCommands.WaitForResponse(command.RequestId, timeout);
try
{
    await _hubContext.Clients.Client(agent.ConnectionId).ExecuteCommand(command);
}
catch (Exception ex)
{
    _pendingCommands.Complete(command.RequestId, new AgentResponse {
        RequestId = command.RequestId,
        Type = ResponseType.Error,
        Success = false,
        ErrorMessage = $"Не удалось отправить команду агенту: {ex.Message}"
    });
}
var response = await responseTask;
```

**Тест:** замокать `IHubContext` на `throw`, проверить, что контроллер возвращается мгновенно, `PendingCommandStore._pending` пустой.

---

### BL-11. `FileSessionManager.FileSession` не thread-safe

**Файл:** `hub-agent/src/TelegramRemoteControl.BotService/FileSessionManager.cs:15-48`

**Симптом:** при быстрых тапах в файловом менеджере (двойной клик по разным папкам) два параллельных `FileCallbackHandler.LoadAndRenderAsync` мутируют одну и ту же `FileSession`. Возможные исходы: `InvalidOperationException` на перечислении `Items`, потерянные записи в `_pathCache`, «залипшие» kb с устаревшими id.

**Корень:** обычный `Dictionary<int, string>` + list без блокировки, несмотря на `ConcurrentDictionary<long, FileSession>` обёртку.

**Фикс:** завести `SemaphoreSlim Lock { get; } = new(1,1)` внутри `FileSession`, обернуть все мутации + load. Аналог `AiSession.Lock`.

**Тест:** sсенарий с 2 параллельными `f:n:*` callback'ами — не должен падать, итоговый `CurrentPath` = последний.

---

### BL-12. Глобальные `ProcessCache` / `ServiceCache` / `WindowCache` без привязки к (userId, agentId)

**Файлы:** `hub-agent/src/TelegramRemoteControl.BotService/ProcessCache.cs`, `ServiceCache.cs`, `WindowCache.cs`

**Симптом:** `/processes` на PC1 заполняет статический кеш PID↔ProcessName. Переключились на PC2 (у того же юзера), кнопка «kill» в старом сообщении шлёт PID, который на PC2 означает совершенно другой процесс. Аналогично для services/windows.

**Фикс:** ключ — `(long userId, string agentId)`. Очищать при `SelectDevice`, при отключении агента (событие), и по TTL.

**Тест:** два ПК у одного юзера, `/processes` на PC1, переключиться на PC2, нажать старую кнопку kill — должно прийти «список устарел, повторите /processes», а не убийство.

---

### BL-13. `PendingCommandStore` — нет CancellationToken

**Файлы:**
- `hub-agent/src/TelegramRemoteControl.Hub/Services/PendingCommandStore.cs`
- `hub-agent/src/TelegramRemoteControl.Hub/Controllers/CommandsController.cs`

**Симптом:** если Bot/клиент отменил HTTP-запрос (timeout HttpClient, пользователь снёс бота), контроллер отваливается, а TCS в `PendingCommandStore` продолжает ждать полные `CommandTimeoutSeconds`. Под нагрузкой — копятся «osiротевшие» TCS.

**Фикс:** передать `HttpContext.RequestAborted` в `WaitForResponse(requestId, timeout, ct)`; зарегистрировать дополнительный `ct.Register(() => tcs.TrySetResult(...))`.

---

### BL-14. SQLite без WAL и `busy_timeout` — `SQLITE_BUSY` под нагрузкой

**Файл:** `hub-agent/src/TelegramRemoteControl.Hub/Data/HubDbContext.cs:24-82` (`InitializeAsync`)

**Симптом:** при 3+ активных пользователях (пишут `UpsertUser`, `SetUserAuthorized`, `UpdateAgentLastSeenAsync`, `AddAuditLog` почти одновременно) параллельные writes конкурируют за journal-lock → sporadic `SqliteException: database is locked`. Контроллер вернёт 500, пользователь увидит «Hub недоступен».

**Фикс:** в `InitializeAsync` после `CREATE TABLE`:
```sql
PRAGMA journal_mode = WAL;
PRAGMA busy_timeout = 5000;
PRAGMA synchronous = NORMAL;
```

**Тест:** нагрузочный тест (50 parallel `/status`) — 0 ошибок; проверить, что появился `hub.db-wal` файл.

---

### BL-15. Admin-callback-handlers не проверяют, что actor — админ

**Файлы:**
- `hub-agent/src/TelegramRemoteControl.BotService/Callbacks/Impl/AdminUserCallbackHandler.cs:15-53`
- `hub-agent/src/TelegramRemoteControl.BotService/Callbacks/Impl/BroadcastCallbackHandler.cs:21-49`

**Симптом:** любой **авторизованный** пользователь, набрав руками (через Bot API, forwarded message или просто ткнув старое сообщение, где его в CC добавили) `admin:approve:123` — одобрит/отклонит другого пользователя. Аналогично `bcast:lock` дёрнет блокировку на всех ПК.

**Корень:** handlers проверяют только `EnsureAuthorizedAsync` (который не различает админа и обычного юзера).

**Фикс:** на входе handler'ов:
```csharp
if (!_settings.AuthorizedUsers.Contains(ctx.UserId))
{
    await AnswerAsync(ctx, "⛔ Только для админов");
    return;
}
```
То же применить в `ApproveCommand` / `DenyCommand` / `BroadcastCommand` (если не стоит).

**Тест:** авторизованный non-admin пытается прислать `admin:approve:999` через кастомный клиент — ответ «только для админов», БД не меняется.

---

### BL-16. Пустой `AuthorizedUsers` → систему не запустить

**Файлы:**
- `hub-agent/src/TelegramRemoteControl.BotService/Commands/Impl/ApproveCommand.cs:46-49`
- `hub-agent/src/TelegramRemoteControl.BotService/BotHandler.cs:527-560` (`NotifyAdminsAboutNewUserAsync`)

**Симптом:** админ не указал `ADMIN_USER_ID` в `.env` / `AuthorizedUsers = []`:
- `IsAdmin` всегда `false` → никто никого не может одобрить;
- `NotifyAdminsAboutNewUserAsync` не отправит уведомление (цикл не итерируется);
- `/register` от нового юзера молча теряется.

**Фикс:** вариант 1 (fail fast) — при старте `TelegramBotService` проверять `AuthorizedUsers.Length > 0`, иначе бросать с понятной ошибкой.

Вариант 2 (bootstrap) — первый зашедший юзер становится админом, если таблица `Users` пуста и `AuthorizedUsers = []`. Риск: первый же случайный зашедший заберёт root.

Рекомендую (1) — явная ошибка лучше молчаливого бага.

**Тест:** запустить бот с пустым `AuthorizedUsers` → приложение падает со стартовой ошибкой, а не принимает команды.

---

### BL-17. `AgentToken` бессрочен + нет отзыва

**Файлы:**
- `hub-agent/src/TelegramRemoteControl.Hub/HubSettings.cs:13` (`AgentTokenTtlDays = 0`)
- `hub-agent/src/TelegramRemoteControl.Hub/Data/HubDbContext.cs` (нет метода `DeleteAgent`)

**Симптом:** токен утёк (через backup `appsettings.json`, украденный диск) → доступ к ПК **навсегда**, без возможности отозвать из UI.

**Фикс:**
1. Дефолт `AgentTokenTtlDays = 90` (с опцией продления через активность — уже есть проверка `LastSeenAt` в `AgentHub.RegisterAgent:54-61`).
2. Команда `/removepc <id>` в боте → `DELETE FROM Agents WHERE AgentId = @id AND OwnerUserId = @user`.
3. Админ-эндпоинт `POST /api/admin/revoke-token` для экстренного отзыва.

**Тест:** `/removepc <id>` → агент при heartbeat получает abort → реконнект с сохранённым токеном → Hub его не находит → agent зависает на «invalid credential».

---

## P2 — Medium

### BL-18. `ShellHelper.RunAsync` — deadlock + сироты-процессы

**Файл:** `hub-agent/src/TelegramRemoteControl.Agent/Execution/ShellHelper.cs:24-36`

**Симптом:**
1. Последовательное `ReadToEndAsync(stdout)` → `ReadToEndAsync(stderr)`. Если процесс выплёвывает >64KB в stderr до исчерпания stdout — он блокируется на буфере stderr, мы ждём stdout → deadlock.
2. При `ct.IsCancellationRequested` выбрасывается `OCE`, но `process.Kill()` не вызывается → `cmd.exe` и его дети остаются висеть.

**Фикс:**
```csharp
var outTask = process.StandardOutput.ReadToEndAsync(ct);
var errTask = process.StandardError.ReadToEndAsync(ct);
using var killReg = ct.Register(() => { try { process.Kill(entireProcessTree: true); } catch { } });
await Task.WhenAll(outTask, errTask);
await process.WaitForExitAsync(ct);
```

---

### BL-19. `EnsureAuthorizedAsync` — DB write на каждое сообщение админа

**Файл:** `hub-agent/src/TelegramRemoteControl.BotService/BotHandler.cs:567-575`

**Симптом:** на каждое сообщение от юзера из `AuthorizedUsers` вызывается `SetUserAuthorized(true)` → `INSERT OR UPDATE` в SQLite. Конкурирует с audit за write-lock (см. BL-14).

**Фикс:** кеш `ConcurrentDictionary<long, bool> _knownAuthorized` — для admin-ов ставим запись в БД один раз, затем только проверяем локальный кеш.

---

### BL-20. `AddAuditLog` — substring может разорвать UTF-16 surrogate pair

**Файл:** `hub-agent/src/TelegramRemoteControl.Hub/Data/HubDbContext.cs:499`

**Симптом:** `arguments[..Math.Min(500, arguments.Length)]` без проверки сурогатной пары. Эмодзи/CJK на границе 500 → `ArgumentException` при вставке в SQLite (или мусорные байты в БД).

**Фикс:** корректная обрезка:
```csharp
static string SafeTruncate(string s, int max)
{
    if (s.Length <= max) return s;
    var end = max;
    if (char.IsHighSurrogate(s[end - 1])) end--;
    return s[..end];
}
```

---

### BL-21. `DeviceStatusMonitor` — flapping без debounce

**Файл:** `hub-agent/src/TelegramRemoteControl.BotService/DeviceStatusMonitor.cs`

**Симптом:** при нестабильной сети агента Hub видит `OnConnected` / `OnDisconnected` каждые 10-30 сек → пользователь получает спам «🟢 подключился / 🔴 отключился» каждую минуту.

**Фикс:** отправлять нотификацию только если статус держится дольше N секунд (напр. 30). Добавить поле `PendingSince` рядом с `_lastStatus`.

---

### BL-22. Обрезка `replyText` в shell-mode ломает Markdown

**Файл:** `hub-agent/src/TelegramRemoteControl.BotService/BotHandler.cs:362-367`

**Симптом:**
```csharp
if (replyText.Length > 4000)
    replyText = replyText[..3950] + "\n...[обрезано]```";
```
Если исходный текст **не** содержал открывающих ```` ``` ````, финальное ```` ``` ```` окажется непарным → Telegram `can't parse entities` → сообщение не доходит.

**Фикс:** резать **до** упаковки в code-block, оборачивать уже обрезанный текст. Либо проверять парность backticks.

---

### BL-23. `BuildAgentInfo` — AgentToken как AgentId

**Файл:** `hub-agent/src/TelegramRemoteControl.Agent/AgentService.cs:229-238`

**Симптом:**
```csharp
AgentId = _settings.AgentToken.Length > 0 ? _settings.AgentToken : Environment.MachineName
```
`AgentInfo.AgentId` передаётся в `_agentManager.Info` и потенциально логируется → токен в логах.

**Фикс:** всегда использовать стабильный идентификатор — `MachineName` или Guid, сохранённый в `appsettings.Agent.DeviceId` при первом старте. Hub сам знает настоящий `AgentId` из БД по токену.

---

### BL-24. `FileListExecutor.ListDirectory` падает на restricted папке целиком

**Файл:** `hub-agent/src/TelegramRemoteControl.Agent/Execution/Executors/FileListExecutor.cs:87-136`

**Симптом:** enumerator `info.EnumerateDirectories()` выбрасывает `UnauthorizedAccessException` на первой же недоступной директории (`C:\System Volume Information`, `$Recycle.Bin`) → весь список падает.

**Фикс:** использовать `new EnumerationOptions { IgnoreInaccessible = true }` в `DirectoryInfo.EnumerateDirectories(...)`.

---

### BL-25. Stale sessions при revoke доступа

**Файлы:** `FileUploadSession`, `AiSessionManager`, `ShellSessionManager`, `WindowTypeSession`, `AiKeyInputSession`

**Симптом:** админ `/deny <userId>` — новые сообщения блокируются `EnsureAuthorizedAsync`, но уже активный in-flight `/ai`-запрос, уже запущенная shell-сессия (ждёт пользовательский текст) — продолжают существовать в памяти бота. После возврата доступа юзер «возвращается» в ту же сессию.

**Фикс:** `UserAuthorizeRequest.Authorized = false` (в `AdminUserCallbackHandler` и `DenyCommand`) + локально вызвать `AiSessionManager.End(userId)`, `FileUploadSession.End(userId)` и т.д. Либо опубликовать событие «user revoked» и повесить подписчиков.

---

### BL-26. Path traversal reminder — shell-команды обходят PathValidator

**Файлы:** `CmdExecutor`, `PowerShellExecutor`

**Симптом:** `FileRootPath` защищает `FileList/Download/Preview`, но через `/cmd type C:\Windows\System32\config\SAM` агент под SYSTEM прочитает что угодно. Это подраздел BL-04, но вынесен отдельно для ясности: файловый sandbox **не ограничивает** shell.

**Фикс:** см. BL-04 (белый список пользователей + подтверждение).

---

### BL-27. `AgentConfigExecutor.Set` — нет синхронизации при concurrent set

**Файл:** `hub-agent/src/TelegramRemoteControl.Agent/Execution/Executors/AgentConfigExecutor.cs:100-155`

**Симптом:** два параллельных `AgentConfig` запроса (теоретически — маловероятно, но возможно) мутируют `_aiSettings` и оба пишут в `appsettings.json`. Результат на диске — неопределён.

**Фикс:** `SemaphoreSlim _setLock` вокруг `ApplySetting` + `PersistSettings`.

---

### BL-28. `ApiKeyMiddleware` — timing-attack-friendly сравнение + пустой ключ = «открыто»

**Файл:** `hub-agent/src/TelegramRemoteControl.Hub/Middleware/ApiKeyMiddleware.cs:18-31`

**Симптом:**
1. `incoming != _apiKey` — обычное string.Equality, с точки зрения timing-атаки не фиксирована. На practice почти невозможно эксплуатировать через TCP, но хорошая гигиена.
2. Пустой `_apiKey` → Hub полностью открыт, без единого warning в логах.

**Фикс:**
1. `CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(incoming), Encoding.UTF8.GetBytes(_apiKey))`.
2. На старте `Program.cs`: `if (string.IsNullOrWhiteSpace(apiKey)) logger.LogWarning("HubSettings.ApiKey not configured — /api is OPEN")`.

---

## P3 — Low / качество кода

### BL-29. AuditLog растёт без retention

Нет очистки `AuditLog`. Через год работы — гигабайты. Добавить background-задачу `DELETE FROM AuditLog WHERE Timestamp < @cutoff` раз в сутки; `cutoff = now - 90 дней`.

---

### BL-30. `HelpCommand` через `IServiceProvider` — хрупкость

**Файл:** `hub-agent/src/TelegramRemoteControl.BotService/Commands/Impl/HelpCommand.cs:9-23`

Текущий workaround для DI deadlock работает, но любой новый `ICommand`, запросивший `CommandRegistry` в конструкторе, воскресит deadlock. Стоит:
1. Вынести `CommandRegistry`/`CallbackRegistry` из `ICommand`-цепочки в отдельные singleton'ы с явным конструктором, принимающим `IEnumerable<ICommand>`, но **без** того, чтобы команды знали про регистри.
2. Оставить паттерн `Lazy<CommandRegistry>` для тех редких случаев, когда команде реально нужен список.
3. Задокументировать в `CLAUDE.md`.

---

### BL-31. `ProxyCommandBase` не передаёт CancellationToken в Hub

**Файл:** `hub-agent/src/TelegramRemoteControl.BotService/Commands/ProxyCommandBase.cs:26-31`

`await ctx.Hub.ExecuteCommand(...)` без `ctx.CancellationToken`. HttpClient может быть отменён через `CancellationTokenSource`, но сейчас этого нет. Добавить перегрузку `HubClient.ExecuteCommand(request, ct)` и пробрасывать.

---

### BL-32. `Encoding.GetEncoding(866)` жёстко прописан для вывода shell

**Файл:** `hub-agent/src/TelegramRemoteControl.Agent/Execution/ShellHelper.cs:19-20`

CP866 — DOS-Russian. Для англ/латиницы работает, для современной Windows 10+ cmd обычно UTF-8 (`chcp 65001`). Если пользователь зашёл под английской локалью или поменял `chcp`, кириллица в выводе «поехала».

**Фикс:** попробовать детектировать через `Console.OutputEncoding.CodePage` (хоть это и не всегда валидно для subprocess), либо дать override через `AgentSettings.ShellOutputEncoding`.

---

### BL-33. Missing cleanup в `DeviceStatusMonitor._lastStatus`

Ключи `{userId}:{agentId}` для снятых с регистрации устройств остаются в словаре навсегда. Память — мелочь, но долгоживущий процесс копит.

---

### BL-34. `AdminController.recent-commands` отдаёт аудит ВСЕХ пользователей любому с API-key

**Файл:** `hub-agent/src/TelegramRemoteControl.Hub/Controllers/AdminController.cs:41-55`

Если когда-нибудь API-key утечёт / Hub выставят наружу — `GET /api/admin/recent-commands` = история команд всех юзеров. Нужен дополнительный уровень auth (отдельный admin-key / JWT).

---

## Сводная таблица

| ID     | Prio | Файл / область                              | Суть                                                                 |
|--------|------|----------------------------------------------|----------------------------------------------------------------------|
| BL-01  | P0   | `BotHandler.cs:585-589`                      | Fail-open при исключении в авторизации ✅                            |
| BL-02  | P0   | `PairController` + БД `PairingRequests`      | Pairing-код plain-text, TTL 180 дней, нет rate-limit ✅              |
| BL-03  | P0   | `PathValidator.cs:39`                        | Prefix collision + дефолт пускает всё ✅                             |
| BL-04  | P0   | `CmdExecutor` / `PowerShellExecutor`         | Command injection под SYSTEM ✅ частично (allowlist, length cap)     |
| BL-05  | P0   | `ConfirmCallbackHandler`                     | Double-execute деструктивных команд                                  |
| BL-06  | P0   | `AgentHub.Heartbeat`                         | Zombie-agent после рестарта Hub                                      |
| BL-07  | P0   | `AgentManager` + `HubSettings`               | Нет liveness — команды висят 120 с на мёртвом агенте                 |
| BL-08  | P0   | `AgentService.ExecuteAsync`                  | Нет retry на первичный `RegisterAgent` failure                       |
| BL-09  | P0   | `AgentService.ReconnectWithTokenAsync`       | Race heartbeat vs reconnect после pairing                            |
| BL-10  | P1   | `CommandsController`, `BroadcastController`  | TCS повисает при исключении `ExecuteCommand`                         |
| BL-11  | P1   | `FileSessionManager.FileSession`             | Не thread-safe — гонка при двойном клике                             |
| BL-12  | P1   | `Process/Service/WindowCache`                | Глобальный state без (userId, agentId) scope                         |
| BL-13  | P1   | `PendingCommandStore`                        | Нет CancellationToken — висят TCS при aborted HTTP                   |
| BL-14  | P1   | `HubDbContext.InitializeAsync`               | SQLite без WAL + busy_timeout                                        |
| BL-15  | P1   | `AdminUserCallbackHandler`, `BroadcastCallbackHandler` | Admin-callbacks не проверяют админа                        |
| BL-16  | P1   | `ApproveCommand` / `NotifyAdmins…`           | Пустой `AuthorizedUsers` → система мертва                            |
| BL-17  | P1   | Hub (agent tokens)                           | Бессрочный `AgentToken` + нет отзыва                                 |
| BL-18  | P2   | `ShellHelper.RunAsync`                       | Deadlock на stderr + сироты-процессы при cancel                      |
| BL-19  | P2   | `EnsureAuthorizedAsync` (admin path)         | `SetUserAuthorized` на каждое сообщение                              |
| BL-20  | P2   | `HubDbContext.AddAuditLog`                   | Substring ломает UTF-16 surrogate pair                               |
| BL-21  | P2   | `DeviceStatusMonitor`                        | Спам-уведомления при flapping                                        |
| BL-22  | P2   | `BotHandler.HandleShellMessageAsync`         | Обрезка ломает Markdown entities                                     |
| BL-23  | P2   | `AgentService.BuildAgentInfo`                | `AgentToken` как `AgentId` → риск утечки в логах                     |
| BL-24  | P2   | `FileListExecutor.ListDirectory`             | Падает на restricted папке целиком                                   |
| BL-25  | P2   | Sessions в BotService                        | Сессии переживают revoke доступа                                     |
| BL-26  | P2   | `CmdExecutor`, `PowerShellExecutor`          | Shell обходит `FileRootPath` sandbox                                 |
| BL-27  | P2   | `AgentConfigExecutor.Set`                    | Нет синхронизации при concurrent set                                 |
| BL-28  | P2   | `ApiKeyMiddleware`                           | Таймингово-зависимое сравнение + пустой ключ = «открыто»             |
| BL-29  | P3   | `HubDbContext` / `AuditLog`                  | Нет retention — растёт без ограничений                               |
| BL-30  | P3   | `HelpCommand` + DI                           | Хрупкая архитектура, легко воскресить deadlock                       |
| BL-31  | P3   | `ProxyCommandBase.ExecuteAsync`              | Нет пробрасывания CancellationToken в Hub                            |
| BL-32  | P3   | `ShellHelper` encoding                       | Hard-coded CP866, ломается на не-RU локалях                          |
| BL-33  | P3   | `DeviceStatusMonitor._lastStatus`            | Нет cleanup удалённых устройств                                      |
| BL-34  | P3   | `AdminController.recent-commands`            | Аудит всех юзеров отдаётся любому с API-key                          |

---

## Рекомендуемый порядок работы

1. **Спринт 1 — стабильность агента (1-2 дня):** BL-06, BL-07, BL-08, BL-09.
   Пользователь перестанет видеть «зависшие» / «фантомные» агенты.
2. **Спринт 2 — авторизация (0.5 дня):** BL-01, BL-15, BL-16.
   Доступ перестаёт протекать, админ-операции защищены.
3. **Спринт 3 — безопасность файлов/shell (1 день):** BL-03, BL-04, BL-26.
   Path traversal закрыт, shell ограничен подтверждением + белым списком.
4. **Спринт 4 — UX-корректность (0.5 дня):** BL-05, BL-11, BL-12.
   Двойные клики и гонки файл-менеджера перестают кусаться.
5. **Спринт 5 — SQLite/retention/токены (1 день):** BL-14, BL-17, BL-29.
6. **Далее** — P2/P3 в фоновом режиме.

Для каждого BL-N при имплементации обновлять этот файл: добавлять статус `✅ исправлено (commit hash)`.
