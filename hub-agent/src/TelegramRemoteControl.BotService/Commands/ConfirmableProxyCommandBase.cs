using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramRemoteControl.BotService.Commands;

public abstract class ConfirmableProxyCommandBase : ProxyCommandBase, IConfirmableCommand
{
    public abstract string ConfirmMessage { get; }

    public virtual string ConfirmActionId => Id;

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        var keyboard = BuildConfirmKeyboard(ConfirmActionId);
        await ctx.Bot.SendMessage(ctx.ChatId, ConfirmMessage,
            parseMode: ParseMode.Markdown,
            replyMarkup: keyboard,
            cancellationToken: ctx.CancellationToken);
    }

    public virtual Task ExecuteConfirmedAsync(CommandContext ctx)
    {
        return base.ExecuteAsync(ctx);
    }

    protected static InlineKeyboardMarkup BuildConfirmKeyboard(string actionId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Да", $"confirm:{actionId}"),
                InlineKeyboardButton.WithCallbackData("❌ Нет", "menu")
            }
        });
    }
}
