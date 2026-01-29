using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.Callbacks;
using TelegramRemoteControl.Commands;
using TelegramRemoteControl.Menu;
using Message = Telegram.Bot.Types.Message;
using CallbackQuery = Telegram.Bot.Types.CallbackQuery;

namespace TelegramRemoteControl;

/// <summary>
/// Главный обработчик сообщений и callback-запросов
/// </summary>
public class BotHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly BotSettings _settings;
    private readonly CommandRegistry _commands;
    private readonly CallbackRegistry _callbacks;
    private readonly MenuBuilder _menu;
    private readonly ILogger _logger;

    public BotHandler(
        ITelegramBotClient bot,
        BotSettings settings,
        CommandRegistry commands,
        CallbackRegistry callbacks,
        MenuBuilder menu,
        ILogger logger)
    {
        _bot = bot;
        _settings = settings;
        _commands = commands;
        _callbacks = callbacks;
        _menu = menu;
        _logger = logger;
    }

    public bool IsAuthorized(long userId) =>
        _settings.AuthorizedUsers.Contains(userId);

    /// <summary>Обработка текстовых сообщений</summary>
    public async Task HandleMessageAsync(Message message, CancellationToken ct)
    {
        if (message.Text is null) return;

        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? 0;

        if (!IsAuthorized(userId))
        {
            await _bot.SendMessage(chatId, $"⛔ Доступ запрещён. Ваш ID: `{userId}`",
                parseMode: ParseMode.Markdown, cancellationToken: ct);
            return;
        }

        var text = message.Text.Trim();

        if (text == "/start" || text == "/menu")
        {
            await ShowMainMenuAsync(chatId, ct);
            return;
        }

        var parts = text.Split(' ', 2);
        var alias = parts[0];
        var args = parts.Length > 1 ? parts[1] : null;

        var command = _commands.FindByAlias(alias);
        if (command == null)
        {
            await _bot.SendMessage(chatId, "❓ Неизвестная команда. /menu", cancellationToken: ct);
            return;
        }

        var context = CreateCommandContext(chatId, userId, args, message, null, ct);
        await command.ExecuteAsync(context);
    }

    /// <summary>Обработка нажатий на кнопки</summary>
    public async Task HandleCallbackAsync(CallbackQuery callback, CancellationToken ct)
    {
        if (callback.Message is null || callback.Data is null) return;

        var chatId = callback.Message.Chat.Id;
        var messageId = callback.Message.MessageId;
        var userId = callback.From.Id;
        var data = callback.Data;

        if (!IsAuthorized(userId))
        {
            await _bot.AnswerCallbackQuery(callback.Id, "⛔ Доступ запрещён", cancellationToken: ct);
            return;
        }

        try
        {
            // Системные callback
            if (await HandleSystemCallback(chatId, messageId, data, callback, ct))
                return;

            // Callback-обработчики (proc:*, и т.д.)
            var (handler, args) = _callbacks.Match(data);
            if (handler != null)
            {
                var ctx = new CallbackContext
                {
                    Bot = _bot,
                    ChatId = chatId,
                    MessageId = messageId,
                    UserId = userId,
                    CallbackId = callback.Id,
                    Args = args,
                    RawData = data,
                    CancellationToken = ct
                };
                await handler.HandleAsync(ctx);
                return;
            }

            // Команды по ID
            var command = _commands.FindById(data);
            if (command != null)
            {
                await ExecuteCommandCallback(command, chatId, messageId, userId, callback, ct);
                return;
            }

            await _bot.AnswerCallbackQuery(callback.Id, "❓ Неизвестное действие", cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling callback '{Data}'", data);
            try
            {
                await _bot.AnswerCallbackQuery(callback.Id, $"❌ {ex.Message}", showAlert: true, cancellationToken: ct);
            }
            catch (Exception notifyEx)
            {
                _logger.LogWarning(notifyEx, "Failed to send error response to callback");
            }
        }
    }

    private async Task<bool> HandleSystemCallback(long chatId, int messageId, string data, CallbackQuery callback, CancellationToken ct)
    {
        switch (data)
        {
            case "menu":
                await EditToMainMenuAsync(chatId, messageId, ct);
                await _bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
                return true;
        }

        if (data.StartsWith("cat:"))
        {
            var category = data[4..];
            await _bot.EditMessageText(chatId, messageId, Categories.GetTitle(category),
                replyMarkup: _menu.CategoryMenu(category), cancellationToken: ct);
            await _bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
            return true;
        }

        if (data.StartsWith("confirm:"))
        {
            var commandId = data[8..];
            var cmd = _commands.FindById(commandId);
            if (cmd != null)
            {
                var context = CreateCommandContext(chatId, callback.From.Id, null, null, callback, ct);
                await cmd.ExecuteAsync(context);
            }
            await _bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
            return true;
        }

        return false;
    }

    private async Task ExecuteCommandCallback(ICommand command, long chatId, int messageId, long userId, CallbackQuery callback, CancellationToken ct)
    {
        if (command is IConfirmableCommand confirmable)
        {
            await _bot.EditMessageText(chatId, messageId, confirmable.ConfirmMessage,
                parseMode: ParseMode.Markdown,
                replyMarkup: _menu.ConfirmMenu(command.Id),
                cancellationToken: ct);
            await _bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
            return;
        }

        await _bot.AnswerCallbackQuery(callback.Id, $"{command.Icon} {command.Title}...", cancellationToken: ct);
        var context = CreateCommandContext(chatId, userId, null, null, callback, ct);
        await command.ExecuteAsync(context);
    }

    private async Task ShowMainMenuAsync(long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(chatId, "🤖 *Пульт управления*\n\nВыберите действие:",
            parseMode: ParseMode.Markdown, replyMarkup: _menu.MainMenu(), cancellationToken: ct);
    }

    private async Task EditToMainMenuAsync(long chatId, int messageId, CancellationToken ct)
    {
        await _bot.EditMessageText(chatId, messageId, "🤖 *Пульт управления*\n\nВыберите действие:",
            parseMode: ParseMode.Markdown, replyMarkup: _menu.MainMenu(), cancellationToken: ct);
    }

    private CommandContext CreateCommandContext(long chatId, long userId, string? arguments, Message? message, CallbackQuery? callback, CancellationToken ct)
    {
        return new CommandContext
        {
            Bot = _bot,
            ChatId = chatId,
            UserId = userId,
            Arguments = arguments,
            Message = message,
            CallbackQuery = callback,
            CancellationToken = ct,
            ReplyWithMenu = text => _bot.SendMessage(chatId, text, parseMode: ParseMode.Markdown, replyMarkup: _menu.MainMenu(), cancellationToken: ct),
            ReplyWithBack = text => _bot.SendMessage(chatId, text, parseMode: ParseMode.Markdown, replyMarkup: _menu.BackButton(), cancellationToken: ct)
        };
    }
}
