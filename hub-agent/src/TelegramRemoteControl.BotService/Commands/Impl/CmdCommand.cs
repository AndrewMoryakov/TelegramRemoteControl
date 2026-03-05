using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class CmdCommand : ICommand
{
    public string Id => "cmd";
    public string[] Aliases => new[] { "/cmd" };
    public string Title => "CMD";
    public string? Icon => "⚡";
    public string? Description => "CMD: режим или разовая команда";
    public string Category => Categories.Shell;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Arguments))
        {
            ShellSessionManager.Start(ctx.UserId, ShellType.Cmd);
            await ctx.Bot.SendMessage(ctx.ChatId,
                "⚡ *CMD режим активирован*\n\n" +
                "Всё, что вы напишете, будет выполнено в CMD на удалённом ПК.\n\n" +
                "Для выхода нажмите кнопку или отправьте /exit",
                parseMode: ParseMode.Markdown,
                replyMarkup: ShellUi.ModeKeyboard("CMD"),
                cancellationToken: ctx.CancellationToken);
            return;
        }

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId      = ctx.UserId,
            CommandType = CommandType.Cmd,
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
