using Telegram.Bot;

namespace TelegramRemoteControl.Callbacks;

/// <summary>
/// Обработчик callback-запросов
/// </summary>
public interface ICallbackHandler
{
    /// <summary>Префикс callback (например "proc:")</summary>
    string Prefix { get; }

    /// <summary>Обработка callback</summary>
    Task HandleAsync(CallbackContext ctx);
}

/// <summary>
/// Контекст callback-запроса
/// </summary>
public class CallbackContext
{
    public ITelegramBotClient Bot { get; init; } = null!;
    public long ChatId { get; init; }
    public int MessageId { get; init; }
    public long UserId { get; init; }
    public string CallbackId { get; init; } = "";
    public CancellationToken CancellationToken { get; init; }

    /// <summary>Данные после префикса (например для "proc:kill:123" это ["kill", "123"])</summary>
    public string[] Args { get; init; } = Array.Empty<string>();

    /// <summary>Полные данные callback</summary>
    public string RawData { get; init; } = "";

    /// <summary>Ответить на callback (toast)</summary>
    public Task AnswerAsync(string? text = null, bool showAlert = false) =>
        Bot.AnswerCallbackQuery(CallbackId, text, showAlert, cancellationToken: CancellationToken);

    /// <summary>Редактировать сообщение</summary>
    public Task EditTextAsync(string text, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup? keyboard = null) =>
        Bot.EditMessageText(ChatId, MessageId, text,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            replyMarkup: keyboard,
            cancellationToken: CancellationToken);
}
