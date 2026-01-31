using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.BotService.Callbacks;
using TelegramRemoteControl.BotService.Commands;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Contracts.HubApi;

namespace TelegramRemoteControl.BotService;

public class BotHandler
{
    private readonly CommandRegistry _commands;
    private readonly CallbackRegistry _callbacks;
    private readonly HubClient _hubClient;
    private readonly MenuBuilder _menu;
    private readonly BotSettings _settings;
    private readonly ILogger<BotHandler> _logger;

    public BotHandler(CommandRegistry commands, CallbackRegistry callbacks, HubClient hubClient,
        MenuBuilder menu, IOptions<BotSettings> settings, ILogger<BotHandler> logger)
    {
        _commands = commands;
        _callbacks = callbacks;
        _hubClient = hubClient;
        _menu = menu;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        if (message.Text == null || message.From == null)
            return;

        var text = message.Text.Trim();
        var parts = text.Split(' ', 2);
        var commandAlias = parts[0];
        var arguments = parts.Length > 1 ? parts[1] : null;
        var isStart = text == "/start" || text == "/menu";
        var isRegister = string.Equals(commandAlias, "/register", StringComparison.OrdinalIgnoreCase);

        var userId = message.From.Id;
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
            return;
        }

        if (isStart)
        {
            await EnsureAutoSelectAsync(userId);
            await ShowMainMenuAsync(bot, message.Chat.Id, userId, ct);
            return;
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
            return seen?.IsAuthorized ?? true;
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
