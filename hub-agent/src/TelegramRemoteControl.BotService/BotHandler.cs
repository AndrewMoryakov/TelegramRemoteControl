using System.Text.Json;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.BotService.Callbacks;
using TelegramRemoteControl.BotService.Commands;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.BotService.Models;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService;

public class BotHandler
{
    private readonly CommandRegistry _commands;
    private readonly CallbackRegistry _callbacks;
    private readonly HubClient _hubClient;
    private readonly MenuBuilder _menu;
    private readonly BotSettings _settings;
    private readonly ILogger<BotHandler> _logger;
    private readonly HashSet<long> _notifiedAbout = new();

    public BotHandler(CommandRegistry commands, CallbackRegistry callbacks, HubClient hubClient,
        MenuBuilder menu, IOptions<BotSettings> settings, ILogger<BotHandler> logger)
    {
        Console.Error.WriteLine("[DIAG] BotHandler ctor: start");
        _commands = commands;
        Console.Error.WriteLine("[DIAG] BotHandler ctor: commands ok");
        _callbacks = callbacks;
        Console.Error.WriteLine("[DIAG] BotHandler ctor: callbacks ok");
        _hubClient = hubClient;
        Console.Error.WriteLine("[DIAG] BotHandler ctor: hubClient ok");
        _menu = menu;
        _settings = settings.Value;
        _logger = logger;
        Console.Error.WriteLine("[DIAG] BotHandler ctor: done");
    }

    public async Task HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        if (message.From == null)
            return;

        var userId = message.From.Id;

        // File upload mode: handle incoming document or photo
        if (FileUploadSession.IsActive(userId) && message.Text == null)
        {
            var isAuthorizedForUpload = await EnsureAuthorizedAsync(message.From, ct);
            if (!isAuthorizedForUpload) return;

            if (message.Document != null || message.Photo != null)
            {
                await HandleFileUploadAsync(bot, message, userId, ct);
                return;
            }
        }

        if (message.Text == null)
            return;

        var text = message.Text.Trim();
        var parts = text.Split(' ', 2);
        var commandAlias = parts[0];
        var arguments = parts.Length > 1 ? parts[1] : null;
        var isStart = text == "/start" || text == "/menu";
        var isRegister = string.Equals(commandAlias, "/register", StringComparison.OrdinalIgnoreCase);

        // userId is already declared above
        var isAuthorized = await EnsureAuthorizedAsync(message.From, ct);
        if (!isAuthorized)
        {
            _logger.LogWarning("Unauthorized access attempt from user {UserId}", userId);
            if (isStart || isRegister)
            {
                await bot.SendMessage(message.Chat.Id,
                    $"⏳ Заявка на доступ создана.\nВаш ID: `{userId}`\n\nОжидайте одобрения администратора.",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct);
            }
            else
            {
                await bot.SendMessage(message.Chat.Id,
                    "⛔ Нет доступа. Отправьте `/register` для запроса доступа.",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct);
            }
            await NotifyAdminsAboutNewUserAsync(bot, message.From, ct);
            return;
        }

        if (isStart)
        {
            AiSessionManager.End(userId);
            await EnsureAutoSelectAsync(userId);
            await ShowMainMenuAsync(bot, message.Chat.Id, userId, ct);
            return;
        }

        // File upload mode: /cancel exits it
        if (FileUploadSession.IsActive(userId) && text == "/cancel")
        {
            FileUploadSession.End(userId);
            await bot.SendMessage(message.Chat.Id, "❌ Загрузка отменена", cancellationToken: ct);
            return;
        }

        // AI key input mode: waiting for OpenRouter API key
        if (AiKeyInputSession.IsActive(userId))
        {
            if (text == "/cancel")
            {
                AiKeyInputSession.End(userId);
                await bot.SendMessage(message.Chat.Id, "❌ Отменено", cancellationToken: ct);
                return;
            }

            if (!text.StartsWith('/'))
            {
                await HandleAiKeyInputAsync(bot, message, userId, ct);
                return;
            }
        }

        // Shell mode: forward text to CMD or PowerShell
        if (ShellSessionManager.IsActive(userId))
        {
            if (text == "/exit")
            {
                ShellSessionManager.End(userId);
                await ShowMainMenuAsync(bot, message.Chat.Id, userId, ct);
                return;
            }

            if (!text.StartsWith('/'))
            {
                await HandleShellMessageAsync(bot, message, userId, ct);
                return;
            }
        }

        // Window type mode: waiting for text to send to a window
        if (WindowTypeSession.IsActive(userId))
        {
            if (text == "/cancel")
            {
                WindowTypeSession.End(userId);
                await bot.SendMessage(message.Chat.Id, "❌ Отменено", cancellationToken: ct);
                return;
            }

            if (!text.StartsWith('/'))
            {
                await HandleWindowTypeMessageAsync(bot, message, userId, ct);
                return;
            }
        }

        // AI mode: forward text to agent
        if (AiSessionManager.IsActive(userId))
        {
            if (text == "/exit")
            {
                AiSessionManager.End(userId);
                await ShowMainMenuAsync(bot, message.Chat.Id, userId, ct);
                return;
            }

            // Don't intercept /commands — let them dispatch normally
            if (!text.StartsWith('/'))
            {
                await HandleAiMessageAsync(bot, message, userId, ct);
                return;
            }
        }

        var command = _commands.FindByAlias(commandAlias);
        if (command == null)
        {
            _logger.LogDebug("Unknown command: {Command}", commandAlias);
            return;
        }

        await EnsureAutoSelectAsync(userId);

        _logger.LogInformation("User {UserId} executing: {Command}", userId, commandAlias);

        var ctx = CreateCommandContext(bot, message.Chat.Id, userId, arguments, ct);

        try
        {
            await command.ExecuteAsync(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command {Command}", commandAlias);
            await bot.SendMessage(message.Chat.Id, $"❌ Ошибка: {ex.Message}", cancellationToken: ct);
        }
    }

    public async Task HandleCallbackAsync(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (query.Data == null || query.From == null)
            return;

        var userId = query.From.Id;
        if (!await EnsureAuthorizedAsync(query.From, ct))
        {
            await TryAnswerCallbackAsync(bot, query.Id, "⛔ Нет доступа", true, ct);
            return;
        }

        _logger.LogInformation("User {UserId} callback: {Data}", userId, query.Data);

        try
        {
            if (await HandleSystemCallbackAsync(bot, query, ct))
                return;

            var handler = _callbacks.FindByData(query.Data);
            if (handler != null)
            {
                var ctx = new CallbackContext
                {
                    Bot = bot,
                    Query = query,
                    UserId = userId,
                    ChatId = query.Message?.Chat.Id ?? userId,
                    Data = query.Data,
                    Hub = _hubClient,
                    CancellationToken = ct
                };
                await handler.HandleAsync(ctx);
                return;
            }

            var command = _commands.FindById(query.Data);
            if (command != null)
            {
                await TryAnswerCallbackAsync(bot, query.Id, $"{command.Icon} {command.Title}...", false, ct);
                var ctx = CreateCommandContext(bot, query.Message?.Chat.Id ?? userId, userId, null, ct);
                await command.ExecuteAsync(ctx);
                return;
            }

            _logger.LogDebug("No callback handler for: {Data}", query.Data);
            await TryAnswerCallbackAsync(bot, query.Id, null, false, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling callback {Data}", query.Data);
            await TryAnswerCallbackAsync(bot, query.Id, $"❌ {ex.Message}", true, ct);
        }
    }

    private async Task HandleFileUploadAsync(ITelegramBotClient bot, Message message, long userId, CancellationToken ct)
    {
        var dest = FileUploadSession.GetDestination(userId);
        FileUploadSession.End(userId);

        string? fileId;
        string? filename;

        if (message.Document != null)
        {
            fileId = message.Document.FileId;
            filename = message.Document.FileName ?? "upload.bin";
        }
        else if (message.Photo != null && message.Photo.Length > 0)
        {
            var largest = message.Photo[^1];
            fileId = largest.FileId;
            filename = "photo.jpg";
        }
        else
        {
            await bot.SendMessage(message.Chat.Id, "❌ Неподдерживаемый тип файла", cancellationToken: ct);
            return;
        }

        await bot.SendChatAction(message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.UploadDocument, cancellationToken: ct);

        try
        {
            var fileInfo = await bot.GetFile(fileId, cancellationToken: ct);
            using var ms = new MemoryStream();
            await bot.DownloadFile(fileInfo.FilePath!, ms, cancellationToken: ct);
            var data = ms.ToArray();

            var response = await _hubClient.ExecuteCommand(new Shared.Contracts.HubApi.ExecuteCommandRequest
            {
                UserId = userId,
                CommandType = Shared.Protocol.CommandType.FileUpload,
                Arguments = dest,
                Parameters = new Dictionary<string, string> { ["filename"] = filename },
                Data = data
            });

            var reply = response.Success
                ? response.Text ?? "✅ Файл загружен"
                : $"❌ {response.ErrorMessage}";

            await bot.SendMessage(message.Chat.Id, reply,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File upload failed for user {UserId}", userId);
            await bot.SendMessage(message.Chat.Id, $"❌ Ошибка загрузки: {ex.Message}", cancellationToken: ct);
        }
    }

    private async Task HandleAiKeyInputAsync(ITelegramBotClient bot, Message message, long userId, CancellationToken ct)
    {
        AiKeyInputSession.End(userId);

        // Delete the message containing the key for security
        try { await bot.DeleteMessage(message.Chat.Id, message.MessageId, cancellationToken: ct); } catch { }

        var response = await _hubClient.ExecuteCommand(new Shared.Contracts.HubApi.ExecuteCommandRequest
        {
            UserId      = userId,
            CommandType = Shared.Protocol.CommandType.AgentConfig,
            Arguments   = $"set:openrouterkey:{message.Text}"
        });

        var reply = response.Success
            ? "✅ API ключ сохранён. Теперь выберите модель через /aiconfig → 📋 Выбрать модель"
            : $"❌ {response.ErrorMessage}";

        await bot.SendMessage(message.Chat.Id, reply, cancellationToken: ct);
    }

    private async Task HandleShellMessageAsync(ITelegramBotClient bot, Message message, long userId, CancellationToken ct)
    {
        var session = ShellSessionManager.Get(userId);
        if (session == null)
            return;

        await bot.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: ct);

        var commandType = session.Type == ShellType.Cmd
            ? CommandType.Cmd
            : CommandType.PowerShell;

        var response = await _hubClient.ExecuteCommand(new Shared.Contracts.HubApi.ExecuteCommandRequest
        {
            UserId      = userId,
            CommandType = commandType,
            Arguments   = message.Text
        });

        var shellLabel = session.Type == ShellType.Cmd ? "CMD" : "PowerShell";
        var keyboard   = ShellUi.ModeKeyboard(shellLabel);

        string replyText;
        if (!response.Success)
            replyText = $"❌ {response.ErrorMessage}";
        else
            replyText = ShellUi.WrapCode(response.Text ?? "(нет вывода)");

        // Truncate if needed, keeping markdown valid
        if (replyText.Length > 4000)
            replyText = replyText[..3950] + "\n...[обрезано]```";

        var parseMode = replyText.Contains("```", StringComparison.Ordinal)
            ? ParseMode.Markdown
            : (ParseMode?)null;

        if (parseMode.HasValue)
            await bot.SendMessage(message.Chat.Id, replyText,
                parseMode: parseMode.Value, replyMarkup: keyboard, cancellationToken: ct);
        else
            await bot.SendMessage(message.Chat.Id, replyText,
                replyMarkup: keyboard, cancellationToken: ct);
    }

    private async Task HandleWindowTypeMessageAsync(ITelegramBotClient bot, Message message, long userId, CancellationToken ct)
    {
        var session = WindowTypeSession.Get(userId);
        if (session == null)
            return;

        WindowTypeSession.End(userId);

        var response = await _hubClient.ExecuteCommand(new Shared.Contracts.HubApi.ExecuteCommandRequest
        {
            UserId = userId,
            CommandType = Shared.Protocol.CommandType.WindowAction,
            Arguments = $"type:{session.Hwnd}:{message.Text}"
        });

        var reply = response.Success ? "✅ Текст отправлен" : $"❌ {response.ErrorMessage}";
        await bot.SendMessage(message.Chat.Id, reply, cancellationToken: ct);
    }

    private async Task HandleAiMessageAsync(ITelegramBotClient bot, Message message, long userId, CancellationToken ct)
    {
        var session = AiSessionManager.Get(userId);
        if (session == null)
            return;

        // Check staleness (30 min)
        if (DateTimeOffset.UtcNow - session.LastMessageAt > TimeSpan.FromMinutes(30))
        {
            AiSessionManager.End(userId);
            await bot.SendMessage(message.Chat.Id,
                "⏰ AI сессия истекла. Начните новую через /ai",
                cancellationToken: ct);
            return;
        }

        await session.Lock.WaitAsync(ct);
        try
        {
            // Typing indicator
            await bot.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: ct);

            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(session.ClaudeSessionId))
                parameters["sessionId"] = session.ClaudeSessionId;

            var request = new ExecuteCommandRequest
            {
                UserId = userId,
                CommandType = CommandType.AiChat,
                Arguments = message.Text,
                Parameters = parameters
            };

            var response = await _hubClient.ExecuteCommand(request);

            if (!response.Success)
            {
                await bot.SendMessage(message.Chat.Id,
                    $"❌ {response.ErrorMessage}",
                    replyMarkup: AiModeKeyboard(),
                    cancellationToken: ct);
                return;
            }

            // Extract session_id from JsonPayload
            if (!string.IsNullOrEmpty(response.JsonPayload))
            {
                try
                {
                    var result = JsonSerializer.Deserialize<ClaudeResultDto>(response.JsonPayload);
                    if (!string.IsNullOrEmpty(result?.SessionId))
                        session.ClaudeSessionId = result.SessionId;
                }
                catch { /* ignore parse errors */ }
            }

            session.LastMessageAt = DateTimeOffset.UtcNow;
            session.MessageCount++;

            await SendAiResponseAsync(bot, message.Chat.Id, response.Text ?? "Нет ответа", ct);
        }
        finally
        {
            session.Lock.Release();
        }
    }

    private async Task SendAiResponseAsync(ITelegramBotClient bot, long chatId, string text, CancellationToken ct)
    {
        var keyboard = AiModeKeyboard();

        if (text.Length <= 4000)
        {
            var parseMode = GetParseMode(text);
            if (parseMode.HasValue)
                await bot.SendMessage(chatId, text, parseMode: parseMode.Value, replyMarkup: keyboard, cancellationToken: ct);
            else
                await bot.SendMessage(chatId, text, replyMarkup: keyboard, cancellationToken: ct);
            return;
        }

        // Split long messages at newlines
        var chunks = new List<string>();
        var remaining = text;
        while (remaining.Length > 0)
        {
            if (remaining.Length <= 4000)
            {
                chunks.Add(remaining);
                break;
            }

            var splitAt = remaining.LastIndexOf('\n', 4000);
            if (splitAt <= 0)
                splitAt = 4000;

            chunks.Add(remaining[..splitAt]);
            remaining = remaining[splitAt..].TrimStart('\n');
        }

        for (int i = 0; i < chunks.Count; i++)
        {
            var isLast = i == chunks.Count - 1;
            var parseMode = GetParseMode(chunks[i]);
            if (isLast)
            {
                if (parseMode.HasValue)
                    await bot.SendMessage(chatId, chunks[i], parseMode: parseMode.Value, replyMarkup: keyboard, cancellationToken: ct);
                else
                    await bot.SendMessage(chatId, chunks[i], replyMarkup: keyboard, cancellationToken: ct);
            }
            else
            {
                if (parseMode.HasValue)
                    await bot.SendMessage(chatId, chunks[i], parseMode: parseMode.Value, cancellationToken: ct);
                else
                    await bot.SendMessage(chatId, chunks[i], cancellationToken: ct);
            }
        }
    }

    private static InlineKeyboardMarkup AiModeKeyboard() => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("🛑 Выйти из AI", "ai:exit"),
            InlineKeyboardButton.WithCallbackData("🔄 Новая сессия", "ai:new")
        }
    });

    private async Task NotifyAdminsAboutNewUserAsync(ITelegramBotClient bot, User user, CancellationToken ct)
    {
        if (_settings.AuthorizedUsers.Length == 0) return;
        lock (_notifiedAbout)
        {
            if (!_notifiedAbout.Add(user.Id)) return; // already notified
        }

        var name = string.IsNullOrWhiteSpace(user.FirstName) ? "—" : user.FirstName;
        var username = user.Username != null ? $"@{user.Username}" : "нет";
        var text = $"👤 *Новый запрос доступа*\n\nID: `{user.Id}`\nИмя: {name}\nUsername: {username}";
        var keyboard = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
        {
            new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("✅ Одобрить", $"admin:approve:{user.Id}"),
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("❌ Отклонить", $"admin:deny:{user.Id}")
            }
        });

        foreach (var adminId in _settings.AuthorizedUsers)
        {
            try
            {
                await bot.SendMessage(adminId, text,
                    parseMode: ParseMode.Markdown,
                    replyMarkup: keyboard,
                    cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify admin {AdminId} about new user {UserId}", adminId, user.Id);
            }
        }
    }

    private async Task<bool> EnsureAuthorizedAsync(User user, CancellationToken ct)
    {
        try
        {
            if (_settings.AuthorizedUsers.Length > 0 && _settings.AuthorizedUsers.Contains(user.Id))
            {
                await _hubClient.SetUserAuthorized(new Shared.Contracts.HubApi.UserAuthorizeRequest
                {
                    UserId = user.Id,
                    Authorized = true
                });
                return true;
            }

            var seen = await _hubClient.ReportUserSeen(new Shared.Contracts.HubApi.UserSeenRequest
            {
                UserId = user.Id,
                Username = user.Username,
                FirstName = user.FirstName
            });
            return seen?.IsAuthorized ?? false; // fail-closed: if Hub unreachable, deny access
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to report user seen");
            return true;
        }
    }

    private async Task<bool> HandleSystemCallbackAsync(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (query.Message == null || string.IsNullOrEmpty(query.Data))
            return false;

        if (query.Data == "menu")
        {
            await EnsureAutoSelectAsync(query.From.Id);
            await EditToMainMenuAsync(bot, query.Message.Chat.Id, query.Message.MessageId, query.From.Id, ct);
            await TryAnswerCallbackAsync(bot, query.Id, null, false, ct);
            return true;
        }

        if (query.Data.StartsWith("cat:"))
        {
            var category = query.Data["cat:".Length..];
            await bot.EditMessageText(query.Message.Chat.Id, query.Message.MessageId, Categories.GetTitle(category),
                replyMarkup: _menu.CategoryMenu(category), cancellationToken: ct);
            await TryAnswerCallbackAsync(bot, query.Id, null, false, ct);
            return true;
        }

        return false;
    }

    private async Task EnsureAutoSelectAsync(long userId)
    {
        try
        {
            var selected = await _hubClient.GetSelectedDevice(userId);
            if (selected != null)
                return;

            var devices = await _hubClient.GetDevices(userId);
            var online = devices.Devices.Where(d => d.IsOnline).ToList();
            if (online.Count == 1)
                await _hubClient.SelectDevice(userId, online[0].AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-select device for user {UserId}", userId);
        }
    }

    private async Task ShowMainMenuAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        var menu = await BuildMainMenuAsync(userId);
        await bot.SendMessage(chatId, "🤖 Пульт управления\n\nВыберите действие:",
            replyMarkup: menu, cancellationToken: ct);
    }

    private async Task EditToMainMenuAsync(ITelegramBotClient bot, long chatId, int messageId, long userId, CancellationToken ct)
    {
        var menu = await BuildMainMenuAsync(userId);
        await bot.EditMessageText(chatId, messageId, "🤖 Пульт управления\n\nВыберите действие:",
            replyMarkup: menu, cancellationToken: ct);
    }

    private async Task<InlineKeyboardMarkup> BuildMainMenuAsync(long userId)
    {
        var selected = await _hubClient.GetSelectedDevice(userId);
        var name = selected == null ? null : GetDeviceName(selected);
        return _menu.MainMenu(name);
    }

    private static string GetDeviceName(DeviceDto device)
    {
        return device.FriendlyName ?? device.MachineName ?? device.AgentId;
    }

    private CommandContext CreateCommandContext(ITelegramBotClient bot, long chatId, long userId, string? arguments, CancellationToken ct)
    {
        return new CommandContext
        {
            Bot = bot,
            ChatId = chatId,
            UserId = userId,
            Arguments = arguments,
            Hub = _hubClient,
            CancellationToken = ct,
            ReplyWithMenu = async text =>
            {
                var menu = await BuildMainMenuAsync(userId);
                var parseMode = GetParseMode(text);
                if (parseMode.HasValue)
                {
                    await bot.SendMessage(chatId, text,
                        parseMode: parseMode.Value,
                        replyMarkup: menu,
                        cancellationToken: ct);
                }
                else
                {
                    await bot.SendMessage(chatId, text,
                        replyMarkup: menu,
                        cancellationToken: ct);
                }
            },
            ReplyWithBack = text =>
            {
                var parseMode = GetParseMode(text);
                if (parseMode.HasValue)
                {
                    return bot.SendMessage(chatId, text,
                        parseMode: parseMode.Value,
                        replyMarkup: _menu.BackButton(),
                        cancellationToken: ct);
                }

                return bot.SendMessage(chatId, text,
                    replyMarkup: _menu.BackButton(),
                    cancellationToken: ct);
            }
        };
    }

    private static ParseMode? GetParseMode(string text)
    {
        return text.Contains("```", StringComparison.Ordinal) ? ParseMode.Markdown : null;
    }

    private async Task TryAnswerCallbackAsync(ITelegramBotClient bot, string callbackId, string? text, bool showAlert, CancellationToken ct)
    {
        try
        {
            await bot.AnswerCallbackQuery(callbackId, text, showAlert, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to answer callback query");
        }
    }
}
