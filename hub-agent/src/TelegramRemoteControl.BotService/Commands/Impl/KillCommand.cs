using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class KillCommand : ProxyCommandBase, IConfirmableCommand
{
    public override string Id => "kill";
    public override string[] Aliases => new[] { "/kill" };
    public override string Title => "Завершить";
    public override string? Icon => "🛑";
    public override string? Description => "Завершить процесс по PID";
    public override string Category => Categories.Control;

    protected override CommandType AgentCommandType => CommandType.Kill;

    public string ConfirmMessage => "🛑 *Завершить процесс?*";

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        if (!TryGetPid(ctx.Arguments, out var pid))
        {
            await ctx.ReplyWithBack("❌ Укажите PID: `/kill 1234`");
            return;
        }

        var text = $"🛑 *Завершить процесс {pid}?*";
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Да", $"confirm:kill:{pid}"),
                InlineKeyboardButton.WithCallbackData("❌ Нет", "menu")
            }
        });

        await ctx.Bot.SendMessage(ctx.ChatId, text,
            parseMode: ParseMode.Markdown,
            replyMarkup: keyboard,
            cancellationToken: ctx.CancellationToken);
    }

    public Task ExecuteConfirmedAsync(CommandContext ctx)
    {
        return base.ExecuteAsync(ctx);
    }

    protected override bool ValidateArguments(CommandContext ctx, out string? errorMessage)
    {
        if (!TryGetPid(ctx.Arguments, out _))
        {
            errorMessage = "❌ Укажите PID: `/kill 1234`";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static bool TryGetPid(string? text, out int pid)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            pid = 0;
            return false;
        }

        return int.TryParse(text.Trim(), out pid);
    }
}
