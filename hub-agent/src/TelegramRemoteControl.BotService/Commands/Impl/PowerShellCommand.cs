using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class PowerShellCommand : ICommand
{
    private readonly BotSettings _settings;

    public PowerShellCommand(IOptions<BotSettings> settings)
    {
        _settings = settings.Value;
    }

    public string Id => "powershell";
    public string[] Aliases => new[] { "/powershell", "/psh" };
    public string Title => "PowerShell";
    public string? Icon => "💠";
    public string? Description => "PS: режим или разовая команда";
    public string Category => Categories.Shell;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ShellAccess.IsAllowed(_settings, ctx.UserId))
        {
            await ctx.ReplyWithBack("⛔ Shell-команды запрещены. Обратитесь к администратору.");
            return;
        }

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

        if (!ShellAccess.TryValidateArgument(_settings, ctx.Arguments, out var argError))
        {
            await ctx.ReplyWithBack(argError!);
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
