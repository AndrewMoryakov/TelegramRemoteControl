using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class PowerShellCommand : ICommand
{
    public string Id => "powershell";
    public string[] Aliases => new[] { "/powershell", "/psh" };
    public string Title => "PowerShell";
    public string? Icon => "💠";
    public string? Description => "PS: режим или разовая команда";
    public string Category => Categories.Shell;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Arguments))
        {
            ShellSessionManager.Start(ctx.UserId, ShellType.PowerShell);
            await ctx.Bot.SendMessage(ctx.ChatId,
                "💠 *PowerShell режим активирован*\n\n" +
                "Всё, что вы напишете, будет выполнено в PowerShell на удалённом ПК.\n\n" +
                "Для выхода нажмите кнопку или отправьте /exit",
                parseMode: ParseMode.Markdown,
                replyMarkup: ShellUi.ModeKeyboard("PowerShell"),
                cancellationToken: ctx.CancellationToken);
            return;
        }

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId      = ctx.UserId,
            CommandType = CommandType.PowerShell,
            Arguments   = ctx.Arguments
        });

        if (!response.Success)
        {
            await ctx.ReplyWithMenu($"❌ {response.ErrorMessage}");
            return;
        }

        await ctx.ReplyWithBack(ShellUi.WrapCode(response.Text ?? "Нет данных"));
    }
}
