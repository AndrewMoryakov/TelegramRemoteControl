# План реализации: Hub-Agent архитектура

> Управление несколькими ПК через один Telegram-бот

---

## Инструкция для LLM-агентов (оркестрация роя)

Этот документ предназначен для параллельного выполнения роем LLM-агентов. Каждый агент получает **один этап** и работает **изолированно**.

### Протокол работы агента

1. **Прочитай** секцию "Архитектура" (общий контекст) и свой этап целиком
2. **Прочитай** существующий код текущего монолита (`src/`) — он содержит логику, которую нужно переиспользовать
3. **Проверь** блок `> AGENT:` в своём этапе — там указаны: скоуп, входы, выходы, запреты
4. **Создавай/модифицируй** ТОЛЬКО файлы из своего скоупа
5. **Не трогай** файлы других этапов — они могут редактироваться параллельно другими агентами
6. **После завершения** — выполни проверку из секции "Проверка"

### Карточка агента (блок `> AGENT:`)

Каждый этап содержит блок:
```
> AGENT:
> Скоуп: файлы, которые агент создаёт/модифицирует (ТОЛЬКО эти)
> Читает: файлы, которые агент читает как контекст (НЕ модифицирует)
> Входные артефакты: что должно существовать ДО начала (результаты зависимостей)
> Выходные артефакты: что агент должен создать/модифицировать
> Запреты: что агент НЕ должен делать
> Контекст: краткий брифинг для агента
```

### Правила изоляции

- **Shared проект** — единственный проект, который редактируется несколькими этапами (1.2, 2.2, 2.5). Оркестратор должен выполнять эти этапы **последовательно**, не параллельно
- **Hub проект** — этапы 1.3, 2.1, 2.2, 2.3 затрагивают разные файлы, но `Program.cs` обновляется в нескольких этапах → оркестратор должен мержить изменения в `Program.cs`
- **Agent проект** — этапы 3.1–3.4 создают РАЗНЫЕ файлы Executors → безопасно параллелить, но `CommandExecutor.cs` обновляется всеми → мерж или последовательно
- **BotService проект** — этапы 3.5–3.8 создают РАЗНЫЕ файлы команд → безопасно параллелить, `CommandRegistry` регистрация → мерж

### Файлы-точки конфликтов (shared state)

| Файл | Этапы, которые его трогают | Стратегия |
|---|---|---|
| `Shared/**` | 1.2, 2.2, 2.5 | Последовательно |
| `Hub/Program.cs` | 1.3, 2.1, 2.3 | Мерж или последовательно |
| `Hub/Hubs/AgentHub.cs` | 1.3, 2.1, 2.2 | Последовательно |
| `Hub/Controllers/CommandsController.cs` | 1.3, 2.3 | Последовательно |
| `Agent/Execution/CommandExecutor.cs` | 1.4, 3.1, 3.2, 3.3, 3.4, 3.9 | Мерж (каждый добавляет свои строки) |
| `Agent/Program.cs` | 1.4 | Только 1.4 |
| `BotService/Program.cs` | 1.5, 2.5, 2.6 | Мерж или последовательно |
| `BotService/BotHandler.cs` | 1.5, 2.5, 2.6, 3.8 | Последовательно |
| `BotService/Commands/CommandRegistry.cs` | 1.5, 2.5, 3.5–3.8, 3.10 | Мерж (регистрация команд) |
| `BotService/Callbacks/CallbackRegistry.cs` | 2.5, 3.11 | Мерж |

### Рекомендуемый порядок запуска

```
Волна 1: 1.1 → 1.2                          (последовательно, основа всего)
Волна 2: 1.3 + 1.4 + 1.5                    (параллельно, разные проекты)
Волна 3: 1.6                                 (E2E проверка)
Волна 4: 2.1 → 2.2 + 2.3 → 2.4 + 2.5 → 2.6 (Hub последовательно, Agent/Bot параллельно где можно)
Волна 5: 3.1 + 3.2 + 3.3 + 3.4 + 3.9       (Agent Executors — параллельно, мерж CommandExecutor)
Волна 6: 3.5 + 3.6 + 3.7 + 3.8 + 3.10      (BotService Proxy — параллельно, мерж Registry)
Волна 7: 3.11                                (callback-обработчики, после Волны 6)
Волна 8: 4.1 + 4.2 + 4.3 + 4.4              (полностью параллельно)
```

### Стратегия мержа для конфликтных файлов

Для `CommandExecutor.cs`, `CommandRegistry.cs`, `CallbackRegistry.cs`:
- Каждый агент добавляет ТОЛЬКО свои строки регистрации
- Оркестратор после завершения волны собирает все добавления в один файл
- Альтернатива: один агент создаёт полный файл регистрации после того, как все Executors/Commands готовы

---

## Архитектура

```
Telegram <-> Bot Service <-> Hub <-> Agent1 (ПК дома)
             (Telegram UI)   (API)   Agent2 (ПК на работе)
                                     Agent3 (ПК друга)
```

| Процесс | Роль | Технология |
|---|---|---|
| **Bot Service** | Telegram polling, UI, меню, рендеринг ответов | Worker Service (.NET 8) |
| **Hub** | REST API, маршрутизация, хранение, SignalR для агентов | ASP.NET Core Web App |
| **Agent** | Исполнение команд на ПК | Worker Service (Windows) |

**Связи:**
- Bot Service <-> Hub: **HTTP REST**
- Hub <-> Agent: **SignalR** (WebSocket + MessagePack)

**Solution:** 4 проекта — Shared, Hub, BotService, Agent

---

## Граф зависимостей этапов

```
1.1 Solution ──┬── 1.2 Shared ──┬── 1.3 Hub ────────── 1.5 E2E проверка
               │                ├── 1.4 Agent ─────────┘        │
               │                └── 1.6 BotService ─────────────┘
               │                                                │
               ├── 2.1 SQLite ── 2.2 Pairing Hub ── 2.4 Pairing Agent
               │                 2.3 Devices API ── 2.5 Devices BotService
               │                                    2.6 Menu ПК
               │                                                │
               ├── 3.1 Info Executors ─── 3.5 Info Proxy ───────┤
               ├── 3.2 Shell Executors ── 3.6 Shell Proxy ─────┤
               ├── 3.3 Screen Executors ─ 3.7 Screen Proxy ────┤
               ├── 3.4 Control Executors  3.8 Control Proxy ───┤
               │   3.9 File Executor ──── 3.10 File Proxy ─────┤
               │   3.11 Callback handlers ──────────────────────┤
               │                                                │
               ├── 4.1 Offline уведомления                     │
               ├── 4.2 Reconnect ──────────────────────────────┤
               ├── 4.3 Аудит                                   │
               └── 4.4 Лимиты                                  │
```

Стрелки = зависимости. Этапы без стрелок между собой — **параллельные**.

---

# Фаза 1 — Скелет

**Цель:** /status проходит полный путь: Telegram -> BotService -> Hub -> Agent -> обратно.

---

## Этап 1.1 — Solution и проекты

**Зависимости:** нет
**Результат:** пустой solution с 4 проектами, которые компилируются

> **AGENT:**
> **Скоуп:** `TelegramRemoteControl.sln`, `src/TelegramRemoteControl.Shared/`, `src/TelegramRemoteControl.Hub/`, `src/TelegramRemoteControl.BotService/`, `src/TelegramRemoteControl.Agent/` — только `.csproj` файлы и пустые `Program.cs`
> **Читает:** ничего
> **Входные артефакты:** нет
> **Выходные артефакты:** solution файл, 4 `.csproj` с NuGet-зависимостями, пустые `Program.cs` заглушки (`// TODO`)
> **Запреты:** НЕ писать бизнес-логику, НЕ создавать классы. Только структура проектов
> **Контекст:** Создаёшь фундамент .NET solution. Shared — `net8.0` classlib. Hub — ASP.NET Core Web App. BotService и Agent — Worker Service. Зависимости проектов: Hub → Shared, BotService → Shared, Agent → Shared

### Что делать

Создать solution:
```
TelegramRemoteControl.sln
├── src/
│   ├── TelegramRemoteControl.Shared/         # net8.0 classlib
│   ├── TelegramRemoteControl.Hub/            # ASP.NET Core Web App
│   ├── TelegramRemoteControl.BotService/     # Worker Service
│   └── TelegramRemoteControl.Agent/          # Worker Service (Windows)
```

Зависимости проектов:
- Hub -> Shared
- BotService -> Shared
- Agent -> Shared

NuGet:
| Проект | Пакеты |
|---|---|
| Shared | нет |
| Hub | Microsoft.AspNetCore.SignalR.Protocols.MessagePack |
| BotService | Telegram.Bot |
| Agent | Microsoft.AspNetCore.SignalR.Client, SignalR.Protocols.MessagePack, Hosting.WindowsServices |

### Проверка
```
dotnet build → успех, 0 ошибок
```

---

## Этап 1.2 — Shared: протокол и контракты

**Зависимости:** 1.1
**Результат:** все DTO и интерфейсы для коммуникации между процессами

> **AGENT:**
> **Скоуп:** `src/TelegramRemoteControl.Shared/**` — все файлы
> **Читает:** ничего из текущего монолита
> **Входные артефакты:** скомпилированный solution из 1.1 (`.csproj` файлы)
> **Выходные артефакты:** `Protocol/AgentCommand.cs`, `AgentResponse.cs`, `CommandType.cs`, `ResponseType.cs`, `AgentInfo.cs`, `ButtonRow.cs`, `ButtonInfo.cs`, `Contracts/IAgentHubServer.cs`, `IAgentHubClient.cs`, `Contracts/HubApi/ExecuteCommandRequest.cs`, `ExecuteCommandResponse.cs`, `DeviceDto.cs`, `DeviceListResponse.cs`
> **Запреты:** НЕ трогать другие проекты. НЕ добавлять NuGet-зависимости (Shared — чистые DTO)
> **Контекст:** Создаёшь все типы данных для коммуникации: Hub↔Agent (SignalR) и BotService↔Hub (REST). Все классы — immutable records/classes с `{ get; init; }`. Enum `CommandType` содержит все типы команд (Status, Processes, Screenshot, Cmd и т.д.). Enum `ResponseType`: Text, Photo, Document, Error, Structured

### Что делать

```
TelegramRemoteControl.Shared/
├── Protocol/
│   ├── AgentCommand.cs
│   ├── AgentResponse.cs
│   ├── CommandType.cs
│   ├── ResponseType.cs
│   ├── AgentInfo.cs
│   ├── ButtonRow.cs
│   └── ButtonInfo.cs
├── Contracts/
│   ├── IAgentHubServer.cs
│   ├── IAgentHubClient.cs
│   └── HubApi/
│       ├── ExecuteCommandRequest.cs
│       ├── ExecuteCommandResponse.cs
│       ├── DeviceDto.cs
│       └── DeviceListResponse.cs
└── TelegramRemoteControl.Shared.csproj
```

**AgentCommand:**
```csharp
public class AgentCommand
{
    public string RequestId { get; init; }       // GUID для корреляции
    public CommandType Type { get; init; }
    public string? Arguments { get; init; }
    public Dictionary<string, string>? Parameters { get; init; }
}
```

**AgentResponse:**
```csharp
public class AgentResponse
{
    public string RequestId { get; init; }
    public ResponseType Type { get; init; }
    public bool Success { get; init; }
    public string? Text { get; init; }
    public string? ErrorMessage { get; init; }
    public byte[]? Data { get; init; }
    public string? FileName { get; init; }
    public string? JsonPayload { get; init; }
    public List<ButtonRow>? Buttons { get; init; }
}
```

**CommandType:**
```csharp
public enum CommandType
{
    Status, Processes, Drives, Ip, Monitor, Uptime,
    Screenshot, WindowsList, WindowAction, WindowScreenshot,
    Cmd, PowerShell,
    Kill, Lock, Services, ServiceAction,
    Shutdown, Restart, Sleep, Hibernate,
    FileList, FileDownload, FilePreview,
    Ping
}
```

**SignalR контракты:**
```csharp
public interface IAgentHubServer
{
    Task RegisterAgent(string agentToken, AgentInfo info);
    Task SendResponse(AgentResponse response);
    Task Heartbeat(AgentInfo info);
}

public interface IAgentHubClient
{
    Task ExecuteCommand(AgentCommand command);
    Task<AgentInfo> Ping();
}
```

**REST DTO (HubApi/):**
```csharp
public class ExecuteCommandRequest
{
    public long UserId { get; init; }
    public CommandType CommandType { get; init; }
    public string? Arguments { get; init; }
    public Dictionary<string, string>? Parameters { get; init; }
}

public class ExecuteCommandResponse
{
    public bool Success { get; init; }
    public ResponseType Type { get; init; }
    public string? Text { get; init; }
    public string? ErrorMessage { get; init; }
    public byte[]? Data { get; init; }
    public string? FileName { get; init; }
    public string? JsonPayload { get; init; }
    public List<ButtonRow>? Buttons { get; init; }
}
```

### Проверка
```
dotnet build → Shared компилируется, все типы доступны в других проектах
```

---

## Этап 1.3 — Hub: минимальный сервер

**Зависимости:** 1.2
**Результат:** Hub принимает подключения агентов по SignalR и отвечает на REST-запросы

> **AGENT:**
> **Скоуп:** `src/TelegramRemoteControl.Hub/**` — `Program.cs`, `Hubs/AgentHub.cs`, `Controllers/CommandsController.cs`, `Services/AgentManager.cs`, `Services/PendingCommandStore.cs`, `HubSettings.cs`, `appsettings.json`
> **Читает:** `src/TelegramRemoteControl.Shared/**` (типы, интерфейсы)
> **Входные артефакты:** скомпилированный Shared проект из 1.2
> **Выходные артефакты:** работающий ASP.NET Core сервер с SignalR endpoint `/agent-hub` и REST endpoint `POST /api/commands/execute`
> **Запреты:** НЕ трогать Shared, Agent, BotService. НЕ добавлять SQLite (это этап 2.1). Аутентификация агентов пока по хардкод-токену
> **Контекст:** Hub — центральный сервер. SignalR хаб принимает подключения агентов, REST контроллер принимает команды от BotService. AgentManager хранит подключённых агентов in-memory. PendingCommandStore — ConcurrentDictionary<RequestId, TaskCompletionSource> для ожидания ответов. Настроить MessagePack и MaximumReceiveMessageSize = 50MB

### Что делать

```
TelegramRemoteControl.Hub/
├── Program.cs
├── Hubs/
│   └── AgentHub.cs
├── Controllers/
│   └── CommandsController.cs
├── Services/
│   ├── AgentManager.cs
│   └── PendingCommandStore.cs
├── appsettings.json
└── TelegramRemoteControl.Hub.csproj
```

**Program.cs:**
- Настроить ASP.NET Core: AddSignalR + AddMessagePackProtocol
- MapHub<AgentHub>("/agent-hub")
- MapControllers
- Настроить MaximumReceiveMessageSize = 50 MB

**AgentHub:**
- `RegisterAgent(token, info)` → сохранить connectionId в AgentManager
- `SendResponse(response)` → вызвать PendingCommandStore.Complete()
- `Heartbeat(info)` → обновить LastSeen
- `OnDisconnectedAsync` → пометить offline

**AgentManager** (ConcurrentDictionary):
- `agentId -> ConnectedAgent { ConnectionId, AgentInfo, IsOnline, LastHeartbeat }`
- `connectionId -> agentId`
- Методы: `SetConnected()`, `SetDisconnected()`, `GetAgent()`, `GetAllAgents()`

**PendingCommandStore:**
- `ConcurrentDictionary<string, TaskCompletionSource<AgentResponse>>`
- `WaitForResponse(requestId, timeout)` → создаёт TCS, ждёт с CancellationTokenSource
- `Complete(requestId, response)` → TrySetResult

**CommandsController:**
```
POST /api/commands/execute
Body: ExecuteCommandRequest
→ AgentManager.GetAllAgents() → первый онлайн (пока без выбора)
→ Создать AgentCommand
→ PendingCommandStore.WaitForResponse(requestId, 120s)
→ AgentHub → Clients.Client(connectionId).ExecuteCommand(cmd)
→ Ждём ответ → маппинг в ExecuteCommandResponse
```

**appsettings.json:**
```json
{
  "HubSettings": {
    "CommandTimeoutSeconds": 120,
    "MaxMessageSizeBytes": 52428800
  }
}
```

### Проверка
```
1. Запустить Hub на http://localhost:5000
2. GET /api/commands/execute → 405 (только POST)
3. SignalR endpoint доступен: ws://localhost:5000/agent-hub
```

---

## Этап 1.4 — Agent: минимальный клиент

**Зависимости:** 1.2 (для компиляции), 1.3 (для запуска)
**Результат:** Agent подключается к Hub, отвечает на команду Status

> **AGENT:**
> **Скоуп:** `src/TelegramRemoteControl.Agent/**` — `Program.cs`, `AgentService.cs`, `AgentSettings.cs`, `Execution/ICommandExecutor.cs`, `Execution/CommandExecutor.cs`, `Execution/Executors/StatusExecutor.cs`, `appsettings.json`
> **Читает:** `src/TelegramRemoteControl.Shared/**` (типы), текущий `src/Commands/Impl/StatusCommand.cs` (логика для переноса)
> **Входные артефакты:** скомпилированный Shared проект из 1.2
> **Выходные артефакты:** Worker Service, подключающийся к Hub по SignalR, выполняющий StatusExecutor
> **Запреты:** НЕ трогать Shared, Hub, BotService. НЕ добавлять все Executors — только StatusExecutor. НЕ добавлять Helpers/Interop (это этапы 3.x)
> **Контекст:** Agent — фоновый сервис на ПК пользователя. Подключается к Hub через SignalR с WithAutomaticReconnect и MessagePack. Получает AgentCommand через `On("ExecuteCommand")`, выполняет через CommandExecutor, отправляет AgentResponse через `InvokeAsync("SendResponse")`. Heartbeat каждые 30 сек. StatusExecutor берёт логику из текущего StatusCommand — Environment.MachineName, OSVersion, ProcessorCount и т.д., но без Telegram — возвращает AgentResponse с текстом

### Что делать

```
TelegramRemoteControl.Agent/
├── Program.cs
├── AgentService.cs
├── AgentSettings.cs
├── Execution/
│   ├── ICommandExecutor.cs
│   ├── CommandExecutor.cs
│   └── Executors/
│       └── StatusExecutor.cs
├── appsettings.json
└── TelegramRemoteControl.Agent.csproj
```

**AgentService (BackgroundService):**
```
1. Создать HubConnection:
   - WithUrl(hubUrl + "/agent-hub")
   - WithAutomaticReconnect([0s, 2s, 5s, 10s, 30s])
   - AddMessagePackProtocol()
2. connection.On<AgentCommand>("ExecuteCommand", async cmd => {
       var response = await _executor.ExecuteAsync(cmd);
       await connection.InvokeAsync("SendResponse", response);
   });
3. ConnectWithRetry loop
4. await connection.InvokeAsync("RegisterAgent", token, agentInfo)
5. Heartbeat loop: каждые 30 сек InvokeAsync("Heartbeat", agentInfo)
6. Reconnected += async _ => await RegisterAgent(...)
```

**CommandExecutor:**
- `Dictionary<CommandType, ICommandExecutor>`
- Пока только `[CommandType.Status] = new StatusExecutor()`
- `ExecuteAsync(cmd)` → найти executor, вызвать, обернуть ошибки

**StatusExecutor** (логика из текущего StatusCommand):
```csharp
public Task<AgentResponse> ExecuteAsync(AgentCommand command)
{
    var text = $"Компьютер: {Environment.MachineName}\n" +
               $"Пользователь: {Environment.UserName}\n" +
               $"ОС: {Environment.OSVersion}\n" +
               $"Uptime: {TimeSpan.FromMilliseconds(Environment.TickCount64)}\n" +
               $"CPU: {Environment.ProcessorCount} ядер";
    return Task.FromResult(new AgentResponse {
        RequestId = command.RequestId,
        Type = ResponseType.Text,
        Success = true,
        Text = text
    });
}
```

**appsettings.json:**
```json
{
  "Agent": {
    "HubUrl": "http://localhost:5000",
    "AgentToken": "test-token-123",
    "FriendlyName": "Dev PC",
    "HeartbeatIntervalSeconds": 30
  }
}
```

### Проверка
```
1. Запустить Hub
2. Запустить Agent → лог: "Connected to Hub", "Registered"
3. Hub лог: агент появился в AgentManager
4. Через 30 сек → heartbeat в логах
```

---

## Этап 1.5 — BotService: минимальный Telegram бот

**Зависимости:** 1.2 (для компиляции), 1.3 (для запуска)
**Результат:** Telegram бот отправляет /status через Hub и показывает ответ

> **AGENT:**
> **Скоуп:** `src/TelegramRemoteControl.BotService/**` — `Program.cs`, `BotSettings.cs`, `TelegramBotService.cs`, `BotHandler.cs`, `HubClient.cs`, `Commands/ICommand.cs`, `Commands/CommandContext.cs`, `Commands/CommandRegistry.cs`, `Commands/Impl/StatusCommand.cs`, `appsettings.json`
> **Читает:** `src/TelegramRemoteControl.Shared/**` (типы), текущий монолит: `src/BotHandler.cs`, `src/Commands/ICommand.cs`, `src/Commands/CommandRegistry.cs` (паттерны для переноса)
> **Входные артефакты:** скомпилированный Shared проект из 1.2
> **Выходные артефакты:** Worker Service с Telegram polling, HubClient (HttpClient → Hub REST), одна команда /status
> **Запреты:** НЕ трогать Shared, Hub, Agent. НЕ добавлять меню, callbacks, MenuBuilder (это этапы 2.x). НЕ добавлять все команды — только StatusCommand. Пока БЕЗ обработки callback-запросов
> **Контекст:** BotService — Telegram UI. Использует polling (как текущий монолит). BotHandler проверяет авторизацию (AuthorizedUsers), роутит через CommandRegistry. HubClient — обёртка HttpClient, вызывает `POST /api/commands/execute` на Hub. CommandContext содержит Bot, ChatId, UserId, Arguments, HubClient. StatusCommand — proxy: отправляет ExecuteCommandRequest через HubClient, получает ExecuteCommandResponse, отправляет текст в Telegram

### Что делать

```
TelegramRemoteControl.BotService/
├── Program.cs
├── BotSettings.cs
├── TelegramBotService.cs
├── BotHandler.cs
├── HubClient.cs
├── Commands/
│   ├── ICommand.cs
│   ├── CommandContext.cs
│   ├── CommandRegistry.cs
│   └── Impl/
│       └── StatusCommand.cs
├── appsettings.json
└── TelegramRemoteControl.BotService.csproj
```

**Program.cs:**
- Host.CreateApplicationBuilder
- Configure<BotSettings>
- AddSingleton<HubClient>
- AddSingleton<CommandRegistry>
- AddHostedService<TelegramBotService>

**TelegramBotService (BackgroundService):**
- Из текущего BotService: polling, SetMyCommands
- HandleUpdateAsync → BotHandler.HandleMessageAsync

**BotHandler:**
- Упрощённый из текущего: авторизация + роутинг через CommandRegistry
- Пока без callback-обработчиков и меню

**HubClient:**
```csharp
public class HubClient
{
    private readonly HttpClient _http;

    public async Task<ExecuteCommandResponse> ExecuteCommand(ExecuteCommandRequest request)
    {
        var resp = await _http.PostAsJsonAsync("/api/commands/execute", request);
        return await resp.Content.ReadFromJsonAsync<ExecuteCommandResponse>();
    }
}
```

**CommandContext** (упрощённый из текущего):
```csharp
public class CommandContext
{
    public ITelegramBotClient Bot { get; init; }
    public long ChatId { get; init; }
    public long UserId { get; init; }
    public string? Arguments { get; init; }
    public HubClient Hub { get; init; }
    public CancellationToken CancellationToken { get; init; }
}
```

**StatusCommand:**
```csharp
public async Task ExecuteAsync(CommandContext ctx)
{
    var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
    {
        UserId = ctx.UserId,
        CommandType = CommandType.Status
    });
    if (response.Success)
        await ctx.Bot.SendMessage(ctx.ChatId, response.Text!);
    else
        await ctx.Bot.SendMessage(ctx.ChatId, $"Ошибка: {response.ErrorMessage}");
}
```

**appsettings.json:**
```json
{
  "BotSettings": {
    "Token": "TELEGRAM_BOT_TOKEN",
    "AuthorizedUsers": [123456789],
    "HubUrl": "http://localhost:5000",
    "HubApiKey": "shared-secret"
  }
}
```

### Проверка
```
1. Запустить Hub + Agent + BotService
2. Отправить /status в Telegram
3. Получить текст с информацией о ПК агента (не сервера!)
```

---

## Этап 1.6 — E2E интеграция и отладка

**Зависимости:** 1.3, 1.4, 1.5
**Результат:** все 3 процесса работают вместе, /status проходит полный цикл

> **AGENT:**
> **Скоуп:** все проекты (review + минимальные фиксы для совместимости)
> **Читает:** весь solution
> **Входные артефакты:** работающие Hub (1.3), Agent (1.4), BotService (1.5)
> **Выходные артефакты:** все 3 процесса работают вместе, /status проходит полный цикл, добавлены логи
> **Запреты:** НЕ добавлять новую функциональность. Только фиксы интеграции, логирование, обработка ошибок
> **Контекст:** Интеграционный этап. Запусти все 3 процесса. Проверь полный цикл: Telegram → BotService → Hub REST → SignalR → Agent → обратно. Отладь таймауты, ошибки сериализации, несовпадения типов. Добавь ILogger в ключевые точки. Проверь поведение при отключении Agent

### Что делать

- Запустить все 3 процесса
- Проверить полный цикл: Telegram -> BotService -> Hub REST -> Hub SignalR -> Agent -> обратно
- Отладить таймауты (Hub ждёт ответ агента до 120 сек)
- Проверить поведение при отключении Agent (таймаут, ошибка в Telegram)
- Проверить reconnect Agent (отключить-подключить)
- Добавить логирование в ключевых точках

### Проверка
```
1. /status → ответ от агента ✓
2. Остановить Agent → /status → "Ошибка: нет доступных агентов" ✓
3. Запустить Agent → /status → ответ ✓
4. Перезапустить Hub → Agent переподключается → /status → ответ ✓
```

---

# Фаза 2 — Привязка и выбор ПК

**Цель:** пользователь привязывает агенты через pairing code и переключается между ними.

---

## Этап 2.1 — Hub: SQLite и модель данных

**Зависимости:** 1.3
**Результат:** Hub хранит зарегистрированных агентов в БД

> **AGENT:**
> **Скоуп:** `src/TelegramRemoteControl.Hub/Data/**` (новые файлы), `Hub/Program.cs` (добавить DI регистрацию), `Hub/Hubs/AgentHub.cs` (обновить RegisterAgent), `Hub/TelegramRemoteControl.Hub.csproj` (NuGet)
> **Читает:** `Hub/Services/AgentManager.cs` (использовать для online-статуса)
> **Входные артефакты:** работающий Hub из 1.3
> **Выходные артефакты:** `Data/HubDbContext.cs`, `Data/AgentRegistration.cs`, `Data/PairingRequest.cs`, `hub.db` создаётся при старте
> **Запреты:** НЕ трогать Agent, BotService, Shared. НЕ создавать контроллеры (это 2.2, 2.3). Пока НЕ менять CommandsController
> **Контекст:** Добавляешь SQLite хранение в Hub. HubDbContext — Singleton, создаёт таблицы при InitializeAsync(). Таблицы: Agents (AgentId, AgentToken, OwnerUserId, MachineName, FriendlyName, RegisteredAt), PairingRequests (Code, UserId, ExpiresAt). Обнови AgentHub.RegisterAgent чтобы проверять токен по БД вместо хардкода

### Что делать

NuGet: Microsoft.Data.Sqlite

```
Hub/Data/
├── HubDbContext.cs
├── AgentRegistration.cs
└── PairingRequest.cs
```

**Таблицы:**
```sql
CREATE TABLE Agents (
    AgentId TEXT PRIMARY KEY,
    AgentToken TEXT NOT NULL UNIQUE,
    OwnerUserId INTEGER NOT NULL,
    MachineName TEXT NOT NULL,
    FriendlyName TEXT,
    RegisteredAt TEXT NOT NULL
);

CREATE TABLE PairingRequests (
    Code TEXT PRIMARY KEY,
    UserId INTEGER NOT NULL,
    ExpiresAt TEXT NOT NULL
);
```

**HubDbContext:**
- InitializeAsync() → создать таблицы если не существуют
- CRUD методы: GetAgentByToken, GetAgentsByUser, AddAgent, GetPairingRequest, etc.
- Зарегистрировать как Singleton в DI

**Обновить AgentHub.RegisterAgent:**
- Проверить agentToken по БД вместо хардкода
- Если токен валиден → загрузить AgentRegistration, установить в AgentManager

### Проверка
```
1. Hub стартует → создаёт hub.db
2. Вручную добавить запись в Agents → Agent подключается по этому токену
```

---

## Этап 2.2 — Hub: Pairing API

**Зависимости:** 2.1
**Результат:** Hub может генерировать pairing codes и привязывать агентов

> **AGENT:**
> **Скоуп:** `Hub/Controllers/PairController.cs` (новый), `Hub/Hubs/AgentHub.cs` (обновить RegisterAgent для pairing), `src/TelegramRemoteControl.Shared/Contracts/IAgentHubClient.cs` (добавить ReceiveToken), `src/TelegramRemoteControl.Shared/Contracts/HubApi/PairRequest.cs`, `PairResponse.cs` (новые)
> **Читает:** `Hub/Data/HubDbContext.cs` (CRUD), `Hub/Services/AgentManager.cs`
> **Входные артефакты:** SQLite из 2.1 (HubDbContext с таблицей PairingRequests)
> **Выходные артефакты:** `POST /api/pair/generate` endpoint, обновлённый AgentHub с pairing flow, метод ReceiveToken в IAgentHubClient
> **Запреты:** НЕ трогать Agent, BotService. НЕ создавать DevicesController (это 2.3)
> **Контекст:** Pairing flow: POST /api/pair/generate генерирует 6-символьный код (A-Z0-9), сохраняет в SQLite с TTL 10 мин. AgentHub.RegisterAgent проверяет: если это AgentToken → обычная аутентификация; если PairingCode → создать AgentRegistration, вернуть токен агенту через ReceiveToken

### Что делать

```
Hub/Controllers/
└── PairController.cs
    POST /api/pair/generate   → { Code, ExpiresAt }
```

**POST /api/pair/generate:**
- Body: `{ UserId }`
- Генерировать 6-символьный код (A-Z0-9)
- Сохранить PairingRequest в SQLite с TTL 10 мин
- Вернуть `{ Code, ExpiresAt }`

**Обновить AgentHub.RegisterAgent:**
```
RegisterAgent(string credential, AgentInfo info):
1. Попробовать как AgentToken → найти в Agents → success
2. Попробовать как PairingCode → найти в PairingRequests:
   a. Проверить ExpiresAt > now
   b. Создать AgentRegistration (новый AgentId, новый Token, OwnerUserId из PairingRequest)
   c. Сохранить в Agents
   d. Удалить PairingRequest
   e. Вернуть AgentToken агенту (через метод на клиенте)
3. Ничего не найдено → разорвать соединение
```

**Обновить Shared — IAgentHubClient:**
```csharp
public interface IAgentHubClient
{
    Task ExecuteCommand(AgentCommand command);
    Task<AgentInfo> Ping();
    Task ReceiveToken(string agentToken);  // НОВЫЙ: Hub отправляет токен после pairing
}
```

### Проверка
```
1. POST /api/pair/generate { UserId: 123 } → { Code: "A7K9M2" }
2. Запустить Agent с PairingCode → Agent подключается, получает токен
3. В БД: запись в Agents с OwnerUserId = 123
```

---

## Этап 2.3 — Hub: Devices API и UserSession

**Зависимости:** 2.1
**Параллельно с:** 2.2
**Результат:** Hub выдаёт список устройств пользователя, позволяет выбрать активное

> **AGENT:**
> **Скоуп:** `Hub/Controllers/DevicesController.cs` (новый), `Hub/Services/UserSessionManager.cs` (новый), `Hub/Controllers/CommandsController.cs` (обновить — использовать UserSessionManager), `Hub/Program.cs` (DI регистрация)
> **Читает:** `Hub/Data/HubDbContext.cs` (метод GetAgentsByUser), `Hub/Services/AgentManager.cs` (IsOnline), `Shared/Contracts/HubApi/DeviceDto.cs`, `DeviceListResponse.cs`
> **Входные артефакты:** SQLite из 2.1, Shared DTO из 1.2
> **Выходные артефакты:** REST endpoints: `GET /api/devices`, `POST /api/devices/select`, `GET /api/devices/selected`. Обновлённый CommandsController с проверкой выбранного агента
> **Запреты:** НЕ трогать Agent, BotService, Shared. НЕ создавать PairController (это 2.2). НЕ менять AgentHub
> **Контекст:** DevicesController объединяет данные из SQLite (зарегистрированные агенты) и AgentManager (online/offline). UserSessionManager — ConcurrentDictionary<long, string> (userId → agentId). CommandsController теперь берёт agentId из UserSessionManager вместо "первый онлайн". Проверяй agent.OwnerUserId == userId перед каждой операцией

### Что делать

```
Hub/Controllers/
└── DevicesController.cs
    GET  /api/devices?userId=123
    POST /api/devices/select  { UserId, AgentId }
    GET  /api/devices/selected?userId=123

Hub/Services/
└── UserSessionManager.cs
```

**UserSessionManager:**
- `ConcurrentDictionary<long, string>` (userId → agentId)
- `GetSelectedAgent(userId)`, `SetSelectedAgent(userId, agentId)`

**GET /api/devices:**
- Загрузить из SQLite: агенты с OwnerUserId == userId
- Дополнить online/offline из AgentManager
- Вернуть `DeviceListResponse`

**POST /api/devices/select:**
- Проверить что агент принадлежит userId
- Сохранить в UserSessionManager

**Обновить CommandsController:**
- Использовать UserSessionManager.GetSelectedAgent(userId)
- Если не выбран → вернуть ошибку "Выберите ПК"
- Проверить agent.OwnerUserId == userId

### Проверка
```
1. GET /api/devices?userId=123 → список агентов с online/offline
2. POST /api/devices/select { UserId: 123, AgentId: "xxx" } → OK
3. POST /api/commands/execute → команда идёт на выбранный агент
4. POST с чужим AgentId → ошибка доступа
```

---

## Этап 2.4 — Agent: поддержка pairing

**Зависимости:** 2.2
**Результат:** Agent при первом запуске привязывается через pairing code

> **AGENT:**
> **Скоуп:** `src/TelegramRemoteControl.Agent/AgentService.cs` (обновить), `Agent/AgentSettings.cs` (обновить если нужно)
> **Читает:** `Shared/Contracts/IAgentHubClient.cs` (метод ReceiveToken), `Agent/appsettings.json`
> **Входные артефакты:** Agent из 1.4, Hub с Pairing API из 2.2 (ReceiveToken в IAgentHubClient)
> **Выходные артефакты:** AgentService поддерживает PairingCode → получение токена → сохранение в appsettings.json
> **Запреты:** НЕ трогать Hub, BotService, Shared. НЕ менять протокол SignalR — использовать существующий ReceiveToken
> **Контекст:** При первом запуске AgentToken пустой, но PairingCode заполнен. AgentService подключается к Hub, вызывает RegisterAgent(pairingCode, agentInfo). Hub через ReceiveToken отправляет сгенерированный AgentToken. Agent сохраняет токен в appsettings.json (File.WriteAllText с JSON), очищает PairingCode. При следующем запуске — использует AgentToken напрямую. connection.On<string>("ReceiveToken", token => ...) для получения

### Что делать

**Обновить AgentService:**
```
1. Если AgentToken не пустой → подключиться с токеном (как раньше)
2. Если AgentToken пустой, но PairingCode есть:
   → подключиться к Hub
   → RegisterAgent(pairingCode, agentInfo)
   → ждать ReceiveToken от Hub
   → сохранить AgentToken в appsettings.json
   → переподключиться с новым токеном
3. Ни того ни другого → ошибка "Укажите PairingCode или AgentToken"
```

**Сохранение токена:**
- Записать `Agent.AgentToken` в appsettings.json
- Очистить `Agent.PairingCode`

### Проверка
```
1. Новый Agent с PairingCode → получает токен → переподключается
2. При следующем запуске → использует токен (PairingCode пустой)
3. Невалидный PairingCode → отключение, ошибка в логе
```

---

## Этап 2.5 — BotService: команды /addpc и /pc

**Зависимости:** 2.2, 2.3
**Результат:** пользователь привязывает и выбирает ПК через Telegram

> **AGENT:**
> **Скоуп:** `BotService/HubClient.cs` (добавить методы), `BotService/Commands/Impl/AddPcCommand.cs` (новый), `BotService/Commands/Impl/SelectPcCommand.cs` (новый), `BotService/Callbacks/` (новая папка: `ICallbackHandler.cs`, `CallbackContext.cs`, `CallbackRegistry.cs`, `Impl/PcCallbackHandler.cs`), `BotService/BotHandler.cs` (добавить callback-обработку), `BotService/Commands/CommandRegistry.cs` (регистрация новых команд), `BotService/Program.cs` (DI), `Shared/Contracts/HubApi/PairRequest.cs`, `PairResponse.cs`, `SelectDeviceRequest.cs` (новые)
> **Читает:** `Shared/Contracts/HubApi/*` (существующие DTO), текущий монолит: `src/Callbacks/CallbackRegistry.cs`, `src/Callbacks/ICallbackHandler.cs` (паттерн для переноса)
> **Входные артефакты:** Hub Pairing API из 2.2, Devices API из 2.3, BotService из 1.5
> **Выходные артефакты:** Команды /addpc и /pc работают, callback pc:select обрабатывается, CallbackRegistry инфраструктура готова
> **Запреты:** НЕ трогать Hub, Agent. НЕ добавлять MenuBuilder (это 2.6). НЕ добавлять другие callback-обработчики кроме PcCallbackHandler
> **Контекст:** Этап создаёт callback-инфраструктуру в BotService (по образцу текущего монолита) и две команды управления ПК. HubClient получает 4 новых метода (GeneratePairCode, GetDevices, SelectDevice, GetSelectedDevice). AddPcCommand — простая команда, показывает код. SelectPcCommand — показывает inline-кнопки с устройствами (🟢/🔴). PcCallbackHandler обрабатывает pc:select:{agentId}. BotHandler обновляется для роутинга CallbackQuery через CallbackRegistry

### Что делать

**Обновить HubClient:**
```csharp
public Task<PairResponse> GeneratePairCode(long userId);
public Task<DeviceListResponse> GetDevices(long userId);
public Task SelectDevice(long userId, string agentId);
public Task<DeviceDto?> GetSelectedDevice(long userId);
```

**Обновить Shared — HubApi/:**
```
├── PairRequest.cs        # { UserId }
├── PairResponse.cs       # { Code, ExpiresAt }
├── SelectDeviceRequest.cs # { UserId, AgentId }
```

**Новые команды:**
```
Commands/Impl/
├── AddPcCommand.cs      # /addpc
└── SelectPcCommand.cs   # /pc
```

**AddPcCommand (/addpc):**
```csharp
public async Task ExecuteAsync(CommandContext ctx)
{
    var result = await ctx.Hub.GeneratePairCode(ctx.UserId);
    await ctx.Bot.SendMessage(ctx.ChatId,
        $"Код привязки: `{result.Code}`\n\n" +
        $"Введите этот код в appsettings.json агента.\n" +
        $"Код действителен 10 минут.");
}
```

**SelectPcCommand (/pc):**
```csharp
public async Task ExecuteAsync(CommandContext ctx)
{
    var devices = await ctx.Hub.GetDevices(ctx.UserId);
    if (devices.Devices.Count == 0) { /* "Нет устройств. /addpc" */ }

    // Inline-кнопки: 🟢/🔴 MachineName
    var buttons = devices.Devices.Select(d =>
        new[] { InlineKeyboardButton.WithCallbackData(
            $"{(d.IsOnline ? "🟢" : "🔴")} {d.FriendlyName ?? d.MachineName}",
            $"pc:select:{d.AgentId}") }
    ).ToArray();

    await ctx.Bot.SendMessage(ctx.ChatId, "🖥 Выберите компьютер:",
        replyMarkup: new InlineKeyboardMarkup(buttons));
}
```

**Callback-обработчик:**
```
Callbacks/Impl/
└── PcCallbackHandler.cs   # prefix: "pc"
    pc:select:{agentId} → Hub.SelectDevice(userId, agentId)
                        → AnswerAsync("Выбран: DESKTOP-WORK")
```

**Добавить CallbackRegistry в BotService** (из текущего проекта):
```
Callbacks/
├── ICallbackHandler.cs
├── CallbackContext.cs
├── CallbackRegistry.cs
└── Impl/
    └── PcCallbackHandler.cs
```

**Обновить BotHandler:**
- Добавить обработку callback-запросов
- Роутинг через CallbackRegistry

### Проверка
```
1. /addpc → код "A7K9M2"
2. Agent с этим кодом → привязывается
3. /pc → кнопка с одним ПК
4. Нажать кнопку → "Выбран: Dev PC"
5. /status → ответ от выбранного агента
```

---

## Этап 2.6 — BotService: меню с выбором ПК

**Зависимости:** 2.5
**Результат:** главное меню показывает текущий ПК, можно переключить

> **AGENT:**
> **Скоуп:** `BotService/Menu/MenuBuilder.cs` (новый), `BotService/Menu/Categories.cs` (новый), `BotService/Commands/CommandContext.cs` (добавить ReplyWithMenu, ReplyWithBack), `BotService/BotHandler.cs` (добавить /menu, авто-выбор), `BotService/Program.cs` (DI)
> **Читает:** текущий монолит: `src/Menu/MenuBuilder.cs`, `src/Menu/Categories.cs` (для переноса), `BotService/HubClient.cs` (GetDevices, GetSelectedDevice)
> **Входные артефакты:** BotService с /addpc, /pc и callback из 2.5
> **Выходные артефакты:** MainMenu с кнопкой выбора ПК, /menu команда, ReplyWithMenu/ReplyWithBack в CommandContext, авто-выбор единственного онлайн-агента
> **Запреты:** НЕ трогать Hub, Agent, Shared. НЕ переделывать существующие команды (только добавить меню-обёртки)
> **Контекст:** Переноси MenuBuilder и Categories из текущего монолита. Добавь верхнюю строку в MainMenu: кнопка с именем текущего ПК (callback "pc:list") или "Выберите ПК" если не выбран. CommandContext получает методы ReplyWithMenu (ответ + кнопка "Меню") и ReplyWithBack (ответ + кнопка "Назад"). Авто-выбор: если у пользователя 1 онлайн-агент и ничего не выбрано → авто-выбрать через HubClient.SelectDevice

### Что делать

**Перенести из текущего проекта:**
- `MenuBuilder.cs` → BotService/Menu/
- `Categories.cs` → BotService/Menu/

**Обновить MenuBuilder:**
- `MainMenu()` → добавить верхнюю строку с текущим ПК:
  ```
  [🖥 DESKTOP-WORK ▾]   ← callback: "pc:list"
  ```
- Если ПК не выбран: `[🖥 Выберите ПК]`

**Обновить BotHandler:**
- Добавить команду /menu → показать MainMenu
- Добавить ReplyWithMenu и ReplyWithBack в CommandContext (как в текущем проекте)

**Обновить StatusCommand и будущие команды:**
- После ответа показывать кнопку "Меню" (как в текущем проекте)

**Авто-выбор:**
- Если у пользователя 1 онлайн-агент и ничего не выбрано → авто-выбор

### Проверка
```
1. /menu → меню с кнопкой ПК сверху
2. Кнопка ПК → список устройств
3. Выбрать → обновлённое меню с именем ПК
```

---

# Фаза 3 — Миграция команд

**Цель:** все 18 команд и 4 callback-обработчика работают через Hub-Agent.

Этапы 3.1–3.4 (Agent Executors) и 3.5–3.8 (BotService Proxy) можно разрабатывать **по группам параллельно**. Внутри каждой группы Agent Executor должен быть готов до BotService Proxy.

---

## Этап 3.1 — Agent: Info Executors

**Зависимости:** 1.4
**Параллельно с:** 3.2, 3.3, 3.4
**Результат:** Agent выполняет информационные команды

> **AGENT:**
> **Скоуп:** `Agent/Execution/Executors/ProcessesExecutor.cs`, `DrivesExecutor.cs`, `IpExecutor.cs`, `MonitorExecutor.cs`, `UptimeExecutor.cs` (все новые), `Agent/Execution/CommandExecutor.cs` (добавить регистрацию)
> **Читает:** текущий монолит: `src/Commands/Impl/ProcessesCommand.cs`, `DrivesCommand.cs`, `IpCommand.cs`, `MonitorCommand.cs`, `UptimeCommand.cs` (логика для переноса), `Agent/Execution/ICommandExecutor.cs`, `Shared/Protocol/*`
> **Входные артефакты:** Agent с StatusExecutor из 1.4
> **Выходные артефакты:** 5 новых Executor-ов, обновлённый CommandExecutor с регистрацией
> **Запреты:** НЕ трогать Hub, BotService, Shared. НЕ создавать Shell/Screen/Control Executors (это 3.2–3.4). Не использовать Telegram.Bot — только AgentResponse
> **Контекст:** Каждый Executor реализует ICommandExecutor. Логику берёшь из текущих команд монолита, но убираешь все ctx.Bot.SendMessage — вместо этого возвращаешь AgentResponse. ProcessesExecutor особенный — возвращает Type=Structured с JsonPayload (массив процессов: pid, name, memory, cpu), чтобы BotService мог строить кнопки. Остальные — Type=Text. Регистрируй в CommandExecutor._executors[CommandType.X] = new XExecutor()

### Что делать

```
Agent/Execution/Executors/
├── StatusExecutor.cs       # уже есть с Этапа 1.4
├── ProcessesExecutor.cs    # ← ProcessesCommand
├── DrivesExecutor.cs       # ← DrivesCommand
├── IpExecutor.cs           # ← IpCommand
├── MonitorExecutor.cs      # ← MonitorCommand
└── UptimeExecutor.cs       # ← UptimeCommand
```

**Перенос логики:**
- Из каждой текущей команды взять тело `ExecuteAsync`
- Убрать все `ctx.Bot.SendMessage(...)` и `ctx.ReplyWithBack(...)`
- Результат → `AgentResponse { Type = Text, Text = "..." }`
- ProcessesExecutor: вернуть `JsonPayload` с массивом процессов (для кнопок на BotService)

**Зарегистрировать в CommandExecutor:**
```csharp
_executors[CommandType.Processes] = new ProcessesExecutor();
_executors[CommandType.Drives] = new DrivesExecutor();
// и т.д.
```

### Проверка
```
POST /api/commands/execute { CommandType: "Processes" } → JSON с процессами
POST /api/commands/execute { CommandType: "Drives" } → текст с дисками
```

---

## Этап 3.2 — Agent: Shell Executors

**Зависимости:** 1.4
**Параллельно с:** 3.1, 3.3, 3.4
**Результат:** Agent выполняет cmd и powershell команды

> **AGENT:**
> **Скоуп:** `Agent/Execution/Executors/ShellExecutor.cs` (новый), `Agent/Helpers/ShellHelper.cs` (новый), `Agent/Execution/CommandExecutor.cs` (добавить регистрацию Cmd, PowerShell)
> **Читает:** текущий монолит: `src/Commands/CommandBase.cs` (метод RunShellAsync), `src/Commands/Impl/CmdCommand.cs`, `PowerShellCommand.cs`
> **Входные артефакты:** Agent из 1.4
> **Выходные артефакты:** ShellHelper (static, переносит RunShellAsync), ShellExecutor (обрабатывает Cmd и PowerShell), регистрация в CommandExecutor
> **Запреты:** НЕ трогать Hub, BotService, Shared. НЕ создавать другие Executors. Добавить ТОЛЬКО свои строки в CommandExecutor
> **Контекст:** ShellHelper извлекает логику RunShellAsync из CommandBase: Process.Start с RedirectStandardOutput/Error, кодировка CP866 (Encoding.GetEncoding(866) из System.Text.Encoding.CodePages). ShellExecutor обрабатывает два типа: Cmd → cmd.exe /c args, PowerShell → powershell.exe -NoProfile -Command args. Результат — AgentResponse { Type=Text, Text=output }. Если вывод пустой — "Команда выполнена без вывода"

### Что делать

```
Agent/Execution/Executors/
└── ShellExecutor.cs        # ← CmdCommand + PowerShellCommand

Agent/Helpers/
└── ShellHelper.cs          # ← CommandBase.RunShellAsync()
```

**ShellHelper** — извлечь из текущего `CommandBase.RunShellAsync()`:
```csharp
public static async Task<string> RunAsync(string fileName, string arguments, CancellationToken ct)
{
    // Process.Start, чтение stdout/stderr, CP866 кодировка
}
```

NuGet Agent: System.Text.Encoding.CodePages (для CP866)

**ShellExecutor:**
- `CommandType.Cmd` → `ShellHelper.RunAsync("cmd.exe", "/c " + arguments)`
- `CommandType.PowerShell` → `ShellHelper.RunAsync("powershell.exe", "-NoProfile -Command " + arguments)`
- Результат → `AgentResponse { Type = Text, Text = output }`

### Проверка
```
POST { CommandType: "Cmd", Arguments: "dir" } → вывод dir
POST { CommandType: "PowerShell", Arguments: "Get-Process" } → вывод
```

---

## Этап 3.3 — Agent: Screen Executors

**Зависимости:** 1.4
**Параллельно с:** 3.1, 3.2, 3.4
**Результат:** Agent делает скриншоты и работает с окнами

> **AGENT:**
> **Скоуп:** `Agent/Execution/Executors/ScreenshotExecutor.cs` (новый), `Agent/Execution/Executors/WindowsExecutor.cs` (новый), `Agent/Helpers/ScreenshotHelper.cs` (копия), `Agent/Helpers/ThumbnailHelper.cs` (копия), `Agent/Interop/SessionInterop.cs` (копия), `Agent/Execution/CommandExecutor.cs` (регистрация)
> **Читает:** текущий монолит: `src/Commands/Impl/ScreenshotCommand.cs`, `src/Commands/Impl/WindowsCommand.cs`, `src/Callbacks/Impl/WindowCallbackHandler.cs`, `src/Helpers/ScreenshotHelper.cs`, `src/Helpers/ThumbnailHelper.cs`, `src/Interop/SessionInterop.cs`
> **Входные артефакты:** Agent из 1.4
> **Выходные артефакты:** ScreenshotExecutor (Type=Photo, Data=bytes), WindowsExecutor (WindowsList→Structured/JSON, WindowAction→Text, WindowScreenshot→Photo), 3 скопированных Helper/Interop файла
> **Запреты:** НЕ трогать Hub, BotService, Shared. НЕ менять логику ScreenshotHelper/SessionInterop — копировать as-is
> **Контекст:** Самый сложный Executor из-за Session 0. ScreenshotExecutor: если в Session 0 → SessionInterop.RunInUserSession() для захвата экрана, иначе прямой вызов ScreenshotHelper. Результат — byte[] PNG. WindowsExecutor обрабатывает 3 CommandType: WindowsList (EnumWindows через PowerShell → JsonPayload с hwnd, title, processName), WindowAction (Parameters["hwnd"] + Parameters["action"]: min/max/restore/close), WindowScreenshot (захват окна по hwnd → Photo bytes)

### Что делать

```
Agent/Execution/Executors/
├── ScreenshotExecutor.cs   # ← ScreenshotCommand (возвращает byte[])
└── WindowsExecutor.cs      # ← WindowsCommand (list, action, screenshot)

Agent/Helpers/
├── ScreenshotHelper.cs     # Без изменений из текущего проекта
└── ThumbnailHelper.cs      # Без изменений

Agent/Interop/
└── SessionInterop.cs       # Без изменений
```

**ScreenshotExecutor:**
- Логика из текущего ScreenshotCommand
- Вместо SendPhoto → `AgentResponse { Type = Photo, Data = File.ReadAllBytes(tempFile) }`

**WindowsExecutor:**
- `CommandType.WindowsList` → PowerShell EnumWindows → JSON
- `CommandType.WindowAction` → Parameters["hwnd"], Parameters["action"]
- `CommandType.WindowScreenshot` → Parameters["hwnd"] → byte[]
- Логика из текущего WindowsCommand и WindowCallbackHandler

### Проверка
```
POST { CommandType: "Screenshot" } → response.Data содержит PNG bytes
POST { CommandType: "WindowsList" } → JsonPayload с окнами
```

---

## Этап 3.4 — Agent: Control Executors

**Зависимости:** 1.4
**Параллельно с:** 3.1, 3.2, 3.3
**Результат:** Agent выполняет управляющие команды

> **AGENT:**
> **Скоуп:** `Agent/Execution/Executors/SystemControlExecutor.cs` (новый), `Agent/Execution/Executors/ServiceExecutor.cs` (новый), `Agent/Execution/CommandExecutor.cs` (регистрация)
> **Читает:** текущий монолит: `src/Commands/Impl/ShutdownCommand.cs`, `src/Commands/Impl/LockCommand.cs`, `src/Commands/Impl/SleepCommand.cs`, `src/Commands/Impl/HibernateCommand.cs`, `src/Commands/Impl/ServicesCommand.cs`, `src/Callbacks/Impl/ServiceCallbackHandler.cs`
> **Входные артефакты:** Agent из 1.4
> **Выходные артефакты:** SystemControlExecutor (Lock, Shutdown, Restart, Sleep, Hibernate, Kill), ServiceExecutor (Services→Structured/JSON, ServiceAction→Text), регистрация в CommandExecutor
> **Запреты:** НЕ трогать Hub, BotService, Shared. НЕ создавать другие Executors
> **Контекст:** SystemControlExecutor обрабатывает 6 CommandType через P/Invoke и Process.Start. Lock → user32.dll LockWorkStation(). Shutdown/Restart → Process.Start("shutdown", "/s|/r /t 10"). Sleep/Hibernate → PowrProf.dll SetSuspendState(). Kill → Process.GetProcessById(int.Parse(Parameters["pid"])).Kill(). ServiceExecutor: Services → ServiceController.GetServices() → JsonPayload с name, displayName, status. ServiceAction → Parameters["name"] + Parameters["action"] (start/stop/restart) → ServiceController.Start()/Stop()

### Что делать

```
Agent/Execution/Executors/
├── SystemControlExecutor.cs # ← Lock, Shutdown, Restart, Sleep, Hibernate
└── ServiceExecutor.cs       # ← ServicesCommand + ServiceCallbackHandler
```

NuGet Agent: System.Management, System.ServiceProcess.ServiceController

**SystemControlExecutor:**
- `CommandType.Lock` → P/Invoke LockWorkStation
- `CommandType.Shutdown` → Process.Start("shutdown", "/s /t 10")
- `CommandType.Restart` → Process.Start("shutdown", "/r /t 10")
- `CommandType.Sleep` → P/Invoke SetSuspendState(false)
- `CommandType.Hibernate` → P/Invoke SetSuspendState(true)
- `CommandType.Kill` → Process.GetProcessById(pid).Kill()

**ServiceExecutor:**
- `CommandType.Services` → ServiceController.GetServices() → JSON
- `CommandType.ServiceAction` → Parameters["name"], Parameters["action"] (start/stop/restart)

### Проверка
```
POST { CommandType: "Services" } → JSON со списком служб
POST { CommandType: "Lock" } → экран агента заблокирован
```

---

## Этап 3.5 — BotService: Info Proxy команды

**Зависимости:** 3.1, 2.6 (для меню)
**Параллельно с:** 3.6, 3.7, 3.8
**Результат:** информационные команды работают через Telegram

> **AGENT:**
> **Скоуп:** `BotService/Commands/ProxyCommandBase.cs` (новый — базовый класс), `BotService/Commands/Impl/StatusCommand.cs` (обновить на ProxyCommandBase), `BotService/Commands/Impl/ProcessesCommand.cs`, `DrivesCommand.cs`, `IpCommand.cs`, `MonitorCommand.cs`, `UptimeCommand.cs` (все новые), `BotService/Commands/CommandRegistry.cs` (регистрация)
> **Читает:** `Shared/Protocol/*`, `Shared/Contracts/HubApi/*`, `BotService/HubClient.cs`, `BotService/Commands/CommandContext.cs`, текущий монолит: `src/Commands/Impl/ProcessesCommand.cs` (рендеринг таблицы)
> **Входные артефакты:** BotService из 2.6, Agent Info Executors из 3.1
> **Выходные артефакты:** ProxyCommandBase (переиспользуемый базовый класс), 6 proxy-команд, обновлённый CommandRegistry
> **Запреты:** НЕ трогать Hub, Agent, Shared. НЕ создавать Shell/Screen/Control команды (это 3.6–3.8). НЕ создавать callback-обработчики (это 3.11)
> **Контекст:** ProxyCommandBase — ключевой абстрактный класс. ExecuteAsync: отправляет ExecuteCommandRequest через HubClient, обрабатывает ошибки, вызывает RenderResponse. RenderResponse по умолчанию: Text→SendMessage, Photo→SendPhoto, Document→SendDocument, Structured→RenderStructured (виртуальный). Большинство команд — однострочные наследники (только Id, Aliases, AgentCommandType). ProcessesCommand переопределяет RenderStructured: десериализует JsonPayload, строит текстовую таблицу, добавляет inline-кнопки proc:kill:pid

### Что делать

**Создать базовый класс:**
```
Commands/
├── ProxyCommandBase.cs     # Базовый proxy: Hub.ExecuteCommand → рендер
```

```csharp
public abstract class ProxyCommandBase : ICommand
{
    protected abstract CommandType AgentCommandType { get; }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = AgentCommandType,
            Arguments = ctx.Arguments
        });
        if (!response.Success) { await ctx.SendError(response.ErrorMessage); return; }
        await RenderResponse(ctx, response);
    }

    protected virtual async Task RenderResponse(CommandContext ctx, ExecuteCommandResponse r) { ... }
    protected virtual Task RenderStructured(CommandContext ctx, ExecuteCommandResponse r) => ...;
}
```

**Команды:**
```
Commands/Impl/
├── StatusCommand.cs        # уже есть, обновить на ProxyCommandBase
├── ProcessesCommand.cs     # Structured → таблица + кнопки Kill
├── DrivesCommand.cs        # Text
├── IpCommand.cs            # Text
├── MonitorCommand.cs       # Text
└── UptimeCommand.cs        # Text
```

Большинство — однострочные:
```csharp
public class DrivesCommand : ProxyCommandBase
{
    public override string Id => "drives";
    public override string[] Aliases => ["/drives"];
    protected override CommandType AgentCommandType => CommandType.Drives;
}
```

**ProcessesCommand** — сложнее, переопределяет `RenderStructured`:
- Десериализовать JsonPayload → список процессов
- Построить таблицу с форматированием
- Добавить inline-кнопки для Kill/Priority

### Проверка
```
/status, /processes, /drives, /ip, /monitor, /uptime → ответы от агента
/processes → кнопки Kill работают (callback)
```

---

## Этап 3.6 — BotService: Shell Proxy команды

**Зависимости:** 3.2
**Параллельно с:** 3.5, 3.7, 3.8
**Результат:** /cmd и /ps работают через Telegram

> **AGENT:**
> **Скоуп:** `BotService/Commands/Impl/CmdCommand.cs` (новый), `BotService/Commands/Impl/PowerShellCommand.cs` (новый), `BotService/Commands/CommandRegistry.cs` (регистрация)
> **Читает:** `BotService/Commands/ProxyCommandBase.cs` (базовый класс из 3.5), текущий монолит: `src/Commands/CommandBase.cs` (SendLongAsync для разбивки)
> **Входные артефакты:** ProxyCommandBase из 3.5, Agent Shell Executors из 3.2
> **Выходные артефакты:** CmdCommand и PowerShellCommand, метод SendLongText (в ProxyCommandBase или CommandContext)
> **Запреты:** НЕ трогать Hub, Agent, Shared. НЕ создавать другие команды
> **Контекст:** Обе команды наследуют ProxyCommandBase, но переопределяют RenderResponse для поддержки длинного вывода. Shell-команды могут возвращать текст > 4096 символов (лимит Telegram). Нужен метод SendLongText: разбивает текст на чанки по 4000 символов (по границе \n), отправляет несколько сообщений. Логику брать из текущего SendLongAsync в CommandBase. CmdCommand: Id="cmd", Aliases=["/cmd"], AgentCommandType=Cmd. PowerShellCommand: Id="ps", Aliases=["/ps","/powershell"], AgentCommandType=PowerShell

### Что делать

```
Commands/Impl/
├── CmdCommand.cs           # Text → SendMessage (длинный текст → разбивка)
└── PowerShellCommand.cs    # Text → SendMessage
```

**Особенности:**
- Результат может быть длинным → реализовать разбивку на части (из текущего `SendLongAsync`)
- Добавить метод `SendLongText` в CommandContext или в ProxyCommandBase

### Проверка
```
/cmd dir C:\ → вывод директории
/ps Get-Process | Select -First 5 → вывод
/cmd ipconfig /all → длинный текст → несколько сообщений
```

---

## Этап 3.7 — BotService: Screen Proxy команды

**Зависимости:** 3.3
**Параллельно с:** 3.5, 3.6, 3.8
**Результат:** /screenshot и /windows работают через Telegram

> **AGENT:**
> **Скоуп:** `BotService/Commands/Impl/ScreenshotCommand.cs` (новый), `BotService/Commands/Impl/WindowsCommand.cs` (новый), `BotService/Commands/CommandRegistry.cs` (регистрация)
> **Читает:** `BotService/Commands/ProxyCommandBase.cs` (базовый класс из 3.5), текущий монолит: `src/Commands/Impl/ScreenshotCommand.cs`, `src/Commands/Impl/WindowsCommand.cs` (UI-рендеринг)
> **Входные артефакты:** ProxyCommandBase из 3.5, Agent Screen Executors из 3.3
> **Выходные артефакты:** ScreenshotCommand (рендерит Photo), WindowsCommand (рендерит Structured с кнопками)
> **Запреты:** НЕ трогать Hub, Agent, Shared. НЕ создавать callback-обработчики для окон (это 3.11)
> **Контекст:** ScreenshotCommand переопределяет RenderResponse: new MemoryStream(response.Data) → ctx.Bot.SendPhoto(chatId, InputFile.FromStream(ms, "screenshot.png")). WindowsCommand переопределяет RenderStructured: десериализует JsonPayload → список окон (hwnd, title, processName), строит inline-кнопки: win:min:{hwnd}, win:max:{hwnd}, win:close:{hwnd}, win:ss:{hwnd}. Каждое окно — строка текста + ряд кнопок действий

### Что делать

```
Commands/Impl/
├── ScreenshotCommand.cs    # Photo → SendPhoto
└── WindowsCommand.cs       # Structured → список окон + кнопки
```

**ScreenshotCommand:**
```csharp
protected override async Task RenderResponse(CommandContext ctx, ExecuteCommandResponse r)
{
    using var ms = new MemoryStream(r.Data!);
    await ctx.Bot.SendPhoto(ctx.ChatId, InputFile.FromStream(ms, "screenshot.png"));
}
```

**WindowsCommand:**
- Десериализовать JsonPayload → список окон
- Показать кнопки: Min, Max, Close, Screenshot для каждого окна

### Проверка
```
/screenshot → фото экрана агента
/windows → список окон с кнопками
Кнопка "Min" → окно свернулось
```

---

## Этап 3.8 — BotService: Control Proxy команды

**Зависимости:** 3.4
**Параллельно с:** 3.5, 3.6, 3.7
**Результат:** управляющие команды работают через Telegram

> **AGENT:**
> **Скоуп:** `BotService/Commands/Impl/KillCommand.cs`, `LockCommand.cs`, `ShutdownCommand.cs`, `RestartCommand.cs`, `SleepCommand.cs`, `HibernateCommand.cs`, `ServicesCommand.cs` (все новые), `BotService/Commands/IConfirmableCommand.cs` (новый), `BotService/BotHandler.cs` (обработка IConfirmableCommand), `BotService/Commands/CommandRegistry.cs` (регистрация)
> **Читает:** `BotService/Commands/ProxyCommandBase.cs` (из 3.5), текущий монолит: `src/Commands/Impl/ShutdownCommand.cs` (IConfirmableCommand паттерн), `src/Commands/Impl/ServicesCommand.cs` (UI-рендеринг)
> **Входные артефакты:** ProxyCommandBase из 3.5, Agent Control Executors из 3.4
> **Выходные артефакты:** 7 proxy-команд, IConfirmableCommand интерфейс, обновлённый BotHandler с подтверждением
> **Запреты:** НЕ трогать Hub, Agent, Shared. НЕ создавать callback-обработчики для сервисов (это 3.11)
> **Контекст:** Shutdown и Restart — IConfirmableCommand: BotHandler при обнаружении этого интерфейса показывает "Вы уверены? [Да] [Нет]" (callback confirm:{commandId} / cancel:{commandId}). Только после подтверждения → выполнение через Hub. ServicesCommand переопределяет RenderStructured: десериализует JsonPayload → таблица сервисов + кнопки svc:start:{name}, svc:stop:{name}, svc:restart:{name}. KillCommand: Arguments содержит PID. Остальные (Lock, Sleep, Hibernate) — простые proxy без переопределений

### Что делать

```
Commands/Impl/
├── KillCommand.cs          # Text
├── LockCommand.cs          # Text
├── ShutdownCommand.cs      # Confirmable → подтверждение
├── RestartCommand.cs       # Confirmable
├── SleepCommand.cs         # Text
├── HibernateCommand.cs     # Text
└── ServicesCommand.cs      # Structured → список + кнопки
```

**Перенести из текущего проекта:**
- `IConfirmableCommand.cs` → BotService/Commands/

**Обновить BotHandler:**
- Обработка IConfirmableCommand (показать подтверждение перед выполнением)
- Callback `confirm:{commandId}` → выполнить команду

**ServicesCommand:**
- Structured → таблица сервисов + кнопки Start/Stop/Restart

### Проверка
```
/shutdown → "Выключить компьютер? [Да] [Нет]" → Да → "Выключение через 10 сек"
/services → список сервисов с кнопками
/lock → экран агента заблокирован
```

---

## Этап 3.9 — Agent: File System Executor

**Зависимости:** 1.4
**Результат:** Agent навигирует по файловой системе и отдаёт файлы

> **AGENT:**
> **Скоуп:** `Agent/Execution/Executors/FileSystemExecutor.cs` (новый), `Agent/Helpers/FileTypeRegistry.cs` (копия из монолита), `Agent/Execution/CommandExecutor.cs` (регистрация FileList, FileDownload, FilePreview)
> **Читает:** текущий монолит: `src/Callbacks/Impl/FileCallbackHandler.cs` (логика навигации, файловых операций), `src/Helpers/FileTypeRegistry.cs`
> **Входные артефакты:** Agent из 1.4
> **Выходные артефакты:** FileSystemExecutor (3 CommandType), FileTypeRegistry (копия), регистрация в CommandExecutor
> **Запреты:** НЕ трогать Hub, BotService, Shared. НЕ хранить сессию навигации — Agent stateless, сессия на BotService
> **Контекст:** FileSystemExecutor обрабатывает 3 типа: FileList → Parameters["path"] (null = список дисков DriveInfo.GetDrives()), возвращает JsonPayload с { Path, Items: [{ Name, IsDirectory, Size, Modified }] }. FileDownload → Parameters["path"], проверить размер < 50MB, File.ReadAllBytes → AgentResponse { Type=Document, Data=bytes, FileName=Path.GetFileName(path) }. FilePreview → Parameters["path"], для текстовых файлов первые 100 строк → Type=Text. FileTypeRegistry копируется as-is — определяет тип файла по расширению

### Что делать

```
Agent/Execution/Executors/
└── FileSystemExecutor.cs

Agent/Helpers/
└── FileTypeRegistry.cs     # Без изменений из текущего проекта
```

**FileSystemExecutor:**
- `CommandType.FileList` → Parameters["path"] (или null для корня/дисков)
  → Вернуть JSON: `{ Path, Items: [{ Name, IsDirectory, Size, Modified }] }`
- `CommandType.FileDownload` → Parameters["path"]
  → `AgentResponse { Type = Document, Data = bytes, FileName = name }`
- `CommandType.FilePreview` → для текстовых файлов → первые N строк

### Проверка
```
POST { CommandType: "FileList", Parameters: { path: "C:\\" } } → JSON с файлами
POST { CommandType: "FileDownload", Parameters: { path: "C:\\test.txt" } } → bytes
```

---

## Этап 3.10 — BotService: File Manager Proxy

**Зависимости:** 3.9, 2.5 (callbacks)
**Результат:** файловый менеджер работает в Telegram

> **AGENT:**
> **Скоуп:** `BotService/Commands/Impl/FilesCommand.cs` (новый), `BotService/Callbacks/Impl/FileCallbackHandler.cs` (новый), `BotService/Services/FileSessionManager.cs` (новый), `BotService/Commands/CommandRegistry.cs` (регистрация), `BotService/Callbacks/CallbackRegistry.cs` (регистрация), `BotService/Program.cs` (DI FileSessionManager)
> **Читает:** текущий монолит: `src/Callbacks/Impl/FileCallbackHandler.cs` (UI-логика навигации, shortId-маппинг), `BotService/HubClient.cs`, `BotService/Callbacks/CallbackContext.cs`
> **Входные артефакты:** Agent FileSystemExecutor из 3.9, BotService callback-инфраструктура из 2.5
> **Выходные артефакты:** FilesCommand, FileCallbackHandler, FileSessionManager (Singleton), регистрация в реестрах
> **Запреты:** НЕ трогать Hub, Agent, Shared. НЕ хранить файловые данные — только сессию навигации
> **Контекст:** Самый сложный callback. FileSessionManager хранит per-user: текущий путь, номер страницы, ConcurrentDictionary<string, string> shortId→fullPath (shortId — 6 символов для callback data, т.к. лимит Telegram 64 байта). FilesCommand: запрашивает FileList(path=null) через Hub → показывает диски как кнопки. FileCallbackHandler: prefix "f", обрабатывает f:nav:{shortId} (навигация), f:dl:{shortId} (скачивание → Hub FileDownload → SendDocument), f:page:{n} (пагинация), f:back (уровень вверх). Каждое действие — round-trip BotService→Hub→Agent→Hub→BotService. Пагинация: 10 элементов на страницу

### Что делать

```
Commands/Impl/
└── FilesCommand.cs

Callbacks/Impl/
└── FileCallbackHandler.cs

Services/
└── FileSessionManager.cs   # Сессия навигации per user
```

**FileSessionManager:**
- `ConcurrentDictionary<long, FileSession>` (userId → session)
- FileSession: текущий путь, номер страницы, кэш shortId → fullPath

**FilesCommand (/files):**
- Запросить `FileList` с path = null (корень) через Hub
- Показать inline-кнопки: диски или файлы/папки

**FileCallbackHandler:**
- `f:nav:{shortId}` → FileSessionManager → fullPath → запросить FileList
- `f:dl:{shortId}` → запросить FileDownload → SendDocument
- `f:page:{n}` → показать страницу N
- `f:back` → перейти на уровень вверх

**Особенности:**
- Самый сложный callback: сессия на BotService, данные с Agent
- Каждое действие — round-trip к агенту

### Проверка
```
/files → список дисков
Нажать "C:" → содержимое C:\
Навигация по папкам → работает
Нажать на файл → скачивание
```

---

## Этап 3.11 — BotService: все callback-обработчики

**Зависимости:** 3.5, 3.7, 3.8 (команды, которые генерируют кнопки)
**Результат:** все inline-кнопки работают

> **AGENT:**
> **Скоуп:** `BotService/Callbacks/Impl/ProcessCallbackHandler.cs` (новый), `BotService/Callbacks/Impl/ServiceCallbackHandler.cs` (новый), `BotService/Callbacks/Impl/WindowCallbackHandler.cs` (новый), `BotService/Callbacks/CallbackRegistry.cs` (регистрация)
> **Читает:** текущий монолит: `src/Callbacks/Impl/ProcessCallbackHandler.cs`, `src/Callbacks/Impl/ServiceCallbackHandler.cs`, `src/Callbacks/Impl/WindowCallbackHandler.cs` (UI-логика), `BotService/HubClient.cs`, `BotService/Callbacks/CallbackContext.cs`
> **Входные артефакты:** BotService команды из 3.5 (ProcessesCommand генерирует proc:* кнопки), 3.7 (WindowsCommand генерирует win:* кнопки), 3.8 (ServicesCommand генерирует svc:* кнопки)
> **Выходные артефакты:** 3 callback-обработчика, регистрация в CallbackRegistry
> **Запреты:** НЕ трогать Hub, Agent, Shared. НЕ менять PcCallbackHandler (2.5) и FileCallbackHandler (3.10) — они уже готовы
> **Контекст:** Каждый handler наследует ICallbackHandler, имеет Prefix и HandleAsync(CallbackContext). ProcessCallbackHandler: prefix="proc", обрабатывает proc:kill:{pid}→Kill CommandType, proc:info:{pid}→текст о процессе. ServiceCallbackHandler: prefix="svc", обрабатывает svc:start:{name}, svc:stop:{name}, svc:restart:{name}→ServiceAction CommandType с Parameters. WindowCallbackHandler: prefix="win", обрабатывает win:min:{hwnd}, win:max:{hwnd}, win:close:{hwnd}→WindowAction, win:ss:{hwnd}→WindowScreenshot. Все отправляют команду через Hub и рендерят ответ (EditMessageText для обновления или SendMessage/SendPhoto для новых данных)

### Что делать

```
Callbacks/Impl/
├── PcCallbackHandler.cs        # уже есть с Этапа 2.5
├── ProcessCallbackHandler.cs   # proc:kill:pid, proc:info:pid, proc:pri:*
├── ServiceCallbackHandler.cs   # svc:start:name, svc:stop:name, svc:restart:name
├── WindowCallbackHandler.cs    # win:min:hwnd, win:close:hwnd, win:ss:hwnd
└── FileCallbackHandler.cs      # уже есть с Этапа 3.10
```

**Каждый обработчик:**
- Парсить callback data (prefix:action:arg)
- Отправить соответствующий CommandType с Parameters через Hub
- Отрендерить ответ (обновить сообщение или отправить новое)

### Проверка
```
/processes → Kill кнопка → процесс убит
/services → Stop кнопка → сервис остановлен
/windows → Min кнопка → окно свёрнуто
```

---

# Фаза 4 — Полировка

**Цель:** надёжность, уведомления, аудит.

Все этапы Фазы 4 **независимы друг от друга**.

---

## Этап 4.1 — Offline уведомления

**Зависимости:** Фаза 2
**Параллельно с:** 4.2, 4.3, 4.4
**Результат:** пользователь получает push при подключении/отключении агента

> **AGENT:**
> **Скоуп:** `BotService/Services/DeviceStatusMonitor.cs` (новый BackgroundService), `BotService/Program.cs` (DI регистрация)
> **Читает:** `BotService/HubClient.cs` (GetDevices), `BotService/BotSettings.cs` (AuthorizedUsers для отправки уведомлений)
> **Входные артефакты:** BotService с HubClient из 2.5, Hub Devices API из 2.3
> **Выходные артефакты:** DeviceStatusMonitor — фоновый сервис, polling каждые 10 сек, push при изменении статуса
> **Запреты:** НЕ трогать Hub, Agent, Shared. НЕ создавать SignalR/webhook — используй простой polling
> **Контекст:** DeviceStatusMonitor — BackgroundService. Каждые 10 сек вызывает HubClient.GetDevices() для каждого userId из AuthorizedUsers. Хранит предыдущее состояние в Dictionary<string, bool> (agentId → isOnline). При изменении: отправляет Telegram-сообщение пользователю ("🟢 {name} подключился" / "🔴 {name} отключился"). При первом запуске — не отправлять уведомления (просто запомнить состояние). Добавить ILogger для отладки

### Что делать

**Подход:** Hub → BotService через SignalR (BotService как второй SignalR клиент).

**Hub:**
```
Hubs/
└── NotificationHub.cs   # Отдельный хаб для BotService
    или использовать REST callback: POST /api/notify на BotService
```

Простой вариант — **REST callback от Hub к BotService:**
- Hub при изменении статуса агента → POST на BotService callback URL
- BotService слушает на своём порту

Или ещё проще — **polling:**
- BotService каждые 10 сек запрашивает GET /api/devices
- Сравнивает с предыдущим состоянием
- При изменении → отправляет сообщение в Telegram

**Что уведомлять:**
- "🟢 DESKTOP-WORK подключился"
- "🔴 DESKTOP-WORK отключился"
- "🆕 Новый компьютер LAPTOP привязан!"

### Проверка
```
1. Отключить Agent → через 5-10 сек сообщение в Telegram "🔴 offline"
2. Запустить Agent → сообщение "🟢 online"
```

---

## Этап 4.2 — Reconnect и надёжность

**Зависимости:** Фаза 1
**Параллельно с:** 4.1, 4.3, 4.4
**Результат:** все компоненты корректно восстанавливаются после сбоев

> **AGENT:**
> **Скоуп:** `Agent/AgentService.cs` (добавить Reconnected/Closed handlers, graceful shutdown), `BotService/HubClient.cs` (retry с exponential backoff), `Hub/Services/HeartbeatChecker.cs` (новый BackgroundService), `Hub/Program.cs` (DI HeartbeatChecker)
> **Читает:** `Hub/Services/AgentManager.cs` (SetDisconnected, LastHeartbeat)
> **Входные артефакты:** все 3 процесса из Фазы 1
> **Выходные артефакты:** Agent с Reconnected/Closed + graceful shutdown, HubClient с retry, HeartbeatChecker
> **Запреты:** НЕ трогать Shared. НЕ менять протокол SignalR. НЕ добавлять новые команды/функциональность
> **Контекст:** 3 компонента. Agent: Reconnected += async _ => await RegisterAgent() (повторная регистрация после переподключения), Closed += async ex => _logger.LogWarning("Disconnected: {ex}"), StopAsync → connection.StopAsync(). BotService HubClient: обернуть все HTTP-вызовы в retry: 3 попытки с экспоненциальной задержкой (1s, 3s, 9s), при полной недоступности → throw с понятным сообщением "Hub недоступен". Hub HeartbeatChecker: BackgroundService, каждые 30 сек проверяет AgentManager.GetAllAgents(), если LastHeartbeat > 90 сек → AgentManager.SetDisconnected(agentId). При старте Hub → все агенты offline (AgentManager пустой)

### Что делать

**Agent:**
- ✅ `WithAutomaticReconnect` уже есть
- Добавить `Reconnected` handler → повторный RegisterAgent
- Добавить `Closed` handler → логирование
- Graceful shutdown: при остановке сервиса → connection.StopAsync()

**BotService:**
- HubClient: retry с экспоненциальной задержкой при HTTP-ошибках
- Если Hub недоступен → сообщение пользователю "Hub недоступен"

**Hub:**
- Heartbeat timeout checker: периодическая задача (каждые 30 сек)
  → если LastHeartbeat > 90 сек → пометить offline
- Валидация состояния при запуске: все агенты offline

### Проверка
```
1. Перезапустить Hub → Agent переподключается → /status работает
2. Сеть Agent пропадает на 5 сек → reconnect → работает
3. Hub недоступен → BotService: "Сервер недоступен, попробуйте позже"
4. Одновременно 2 агента → оба reconnect корректно
```

---

## Этап 4.3 — Аудит (логирование команд)

**Зависимости:** 2.1 (SQLite)
**Параллельно с:** 4.1, 4.2, 4.4
**Результат:** все выполненные команды записываются в БД

> **AGENT:**
> **Скоуп:** `Hub/Data/HubDbContext.cs` (добавить таблицу CommandLog + методы LogCommand, GetCommandHistory), `Hub/Controllers/CommandsController.cs` (добавить логирование после выполнения), опционально: `BotService/Commands/Impl/HistoryCommand.cs` (новый), `BotService/HubClient.cs` (добавить GetHistory), `Hub/Controllers/CommandsController.cs` (GET /api/commands/history), `Shared/Contracts/HubApi/CommandLogEntry.cs` (новый DTO)
> **Читает:** `Hub/Data/HubDbContext.cs` (существующая структура), `Hub/Controllers/CommandsController.cs` (точка вставки логирования)
> **Входные артефакты:** Hub с SQLite из 2.1, CommandsController из 1.3/2.3
> **Выходные артефакты:** Таблица CommandLog, автоматическое логирование всех команд, опционально команда /history
> **Запреты:** НЕ трогать Agent. Логирование НЕ должно блокировать ответ (fire-and-forget или Task.Run)
> **Контекст:** Добавить CREATE TABLE CommandLog в HubDbContext.InitializeAsync(). В CommandsController после получения ответа от агента → _ = Task.Run(() => _db.LogCommand(userId, agentId, commandType, arguments, success, errorMessage)). Метод GetCommandHistory(userId, limit=20) → SELECT ... ORDER BY ExecutedAt DESC LIMIT @limit. Опциональный /history: BotService → Hub GET /api/commands/history?userId=X → форматированный список "14:30 Screenshot ✅", "14:28 Cmd 'dir' ✅"

### Что делать

**Hub — таблица:**
```sql
CREATE TABLE CommandLog (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    AgentId TEXT NOT NULL,
    CommandType TEXT NOT NULL,
    Arguments TEXT,
    Success INTEGER NOT NULL,
    ErrorMessage TEXT,
    ExecutedAt TEXT NOT NULL
);
```

**Обновить CommandsController:**
- После получения ответа от агента → записать в CommandLog
- Async, не блокировать ответ

**Опционально — BotService команда /history:**
- Показать последние 20 команд пользователя

### Проверка
```
1. Выполнить 5 команд
2. SELECT * FROM CommandLog → 5 записей
3. /history → список последних команд
```

---

## Этап 4.4 — Лимиты и защита

**Зависимости:** Фаза 1
**Параллельно с:** 4.1, 4.2, 4.3
**Результат:** система защищена от злоупотреблений

> **AGENT:**
> **Скоуп:** `Agent/Execution/CommandExecutor.cs` (добавить timeout на выполнение, проверку размера), `Hub/Controllers/CommandsController.cs` (добавить rate limiting), `Hub/Controllers/PairController.cs` (добавить проверку лимита агентов), `Hub/Data/HubDbContext.cs` (метод GetAgentCountByUser), `BotService/BotHandler.cs` (добавить ограничение длины Arguments)
> **Читает:** `Hub/appsettings.json` (HubSettings: MaxAgentsPerUser, CommandTimeoutSeconds), `Agent/appsettings.json`
> **Входные артефакты:** все 3 процесса из Фазы 1, Hub с SQLite из 2.1
> **Выходные артефакты:** Agent: timeout 120s на ExecuteAsync + проверка Data.Length < 50MB. Hub: rate limit (ConcurrentDictionary<userId, Queue<DateTime>> — max 30/мин), max 10 агентов на пользователя. BotService: Arguments.Length ≤ 4000
> **Запреты:** НЕ трогать Shared. НЕ добавлять новую функциональность — только защитные проверки
> **Контекст:** Agent CommandExecutor.ExecuteAsync: обернуть вызов executor в Task.WhenAny(executorTask, Task.Delay(120s)), при таймауте → AgentResponse { Success=false, ErrorMessage="Таймаут выполнения команды" }. Перед отправкой ответа: если Data?.Length > 50MB → ошибка. Hub PairController: перед генерацией кода → _db.GetAgentCountByUser(userId) ≥ MaxAgentsPerUser → 400 "Достигнут лимит устройств". Rate limit: простой in-memory счётчик, не нужен Redis. BotService BotHandler: если message.Text.Length > 4000 → "Слишком длинная команда"

### Что делать

**Agent:**
- Максимальный размер ответа: 50 MB
  → Если файл больше → ошибка "Файл слишком большой"
- Timeout на выполнение команды: 120 сек (из AgentCommand.Timeout или дефолт)

**Hub:**
- Max агентов на пользователя: 10 (проверка при pairing)
- Command timeout: 120 сек (настройка)
- Rate limiting: max 30 команд в минуту на пользователя (опционально)

**BotService:**
- Проверка AuthorizedUsers перед любой обработкой
- Ограничение длины Arguments (max 4000 символов)

### Проверка
```
1. Попробовать привязать 11-й агент → ошибка
2. Скачать файл > 50 MB → ошибка "Файл слишком большой"
3. Команда без ответа 120+ сек → таймаут
```

---

## Конфигурация

### BotService — appsettings.json
```json
{
  "BotSettings": {
    "Token": "TELEGRAM_BOT_TOKEN",
    "AuthorizedUsers": [123456789],
    "HubUrl": "http://localhost:5000",
    "HubApiKey": "shared-secret"
  }
}
```

### Hub — appsettings.json
```json
{
  "HubSettings": {
    "DatabasePath": "hub.db",
    "ApiKey": "shared-secret",
    "AgentTimeoutSeconds": 90,
    "CommandTimeoutSeconds": 120,
    "MaxMessageSizeBytes": 52428800,
    "MaxAgentsPerUser": 10
  }
}
```

### Agent — appsettings.json
```json
{
  "Agent": {
    "HubUrl": "https://my-hub.example.com",
    "AgentToken": "",
    "PairingCode": "",
    "FriendlyName": "Рабочий ПК",
    "HeartbeatIntervalSeconds": 30
  }
}
```

---

## NuGet пакеты

| Проект | Пакеты |
|---|---|
| Shared | нет |
| Hub | Microsoft.AspNetCore.SignalR.Protocols.MessagePack, Microsoft.Data.Sqlite |
| BotService | Telegram.Bot |
| Agent | Microsoft.AspNetCore.SignalR.Client, SignalR.Protocols.MessagePack, Hosting.WindowsServices, System.Management, System.ServiceProcess.ServiceController, System.Text.Encoding.CodePages |

---

## Миграция текущего кода

| Текущий файл | Куда | Что делать |
|---|---|---|
| `BotHandler.cs` | BotService | Адаптировать: proxy через HubClient |
| `CommandBase.cs` | Agent (ShellHelper) + BotService (ProxyCommandBase) | Разделить |
| `ICommand.cs`, `CommandContext.cs` | BotService | CommandContext += HubClient |
| `CommandRegistry.cs` | BotService | Без изменений |
| `MenuBuilder.cs` | BotService | + строка выбора ПК |
| `Categories.cs` | BotService | Без изменений |
| `StatusCommand.cs` и др. | BotService (proxy) + Agent (executor) | Логика → Executor |
| `ProcessCallbackHandler.cs` и др. | BotService (proxy) | Proxy через Hub |
| `SessionInterop.cs` | Agent | Без изменений |
| `ScreenshotHelper.cs` | Agent | Без изменений |
| `ThumbnailHelper.cs` | Agent | Без изменений |
| `FfmpegProvider.cs` | Agent | Без изменений |
| `FileTypeRegistry.cs` | Agent | Без изменений |
| `BotSettings.cs` | Разделить на 3 | BotSettings, HubSettings, AgentSettings |
