using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramRemoteControl.BotService.Callbacks;

public class CallbackContext
{
    public required ITelegramBotClient Bot { get; init; }
    public required CallbackQuery Query { get; init; }
    public required long UserId { get; init; }
    public required long ChatId { get; init; }
    public required string Data { get; init; }
    public required HubClient Hub { get; init; }
    public CancellationToken CancellationToken { get; init; }

    public int? MessageId => Query.Message?.MessageId;
}
