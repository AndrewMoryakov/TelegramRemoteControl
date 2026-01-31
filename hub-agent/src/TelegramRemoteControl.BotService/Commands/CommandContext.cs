using Telegram.Bot;

namespace TelegramRemoteControl.BotService.Commands;

public class CommandContext
{
    public required ITelegramBotClient Bot { get; init; }
    public required long ChatId { get; init; }
    public required long UserId { get; init; }
    public string? Arguments { get; init; }
    public required HubClient Hub { get; init; }
    public CancellationToken CancellationToken { get; init; }

    /// <summary>Отправить ответ с главным меню</summary>
    public required Func<string, Task> ReplyWithMenu { get; init; }

    /// <summary>Отправить ответ с кнопкой "Назад"</summary>
    public required Func<string, Task> ReplyWithBack { get; init; }
}
