using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class PowerShellCommand : ProxyCommandBase
{
    public override string Id => "powershell";
    public override string[] Aliases => new[] { "/powershell", "/psh" };
    public override string Title => "PowerShell";
    public override string? Icon => "💠";
    public override string? Description => "Выполнить команду PowerShell";
    public override string Category => Categories.Shell;

    protected override CommandType AgentCommandType => CommandType.PowerShell;

    protected override bool ValidateArguments(CommandContext ctx, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(ctx.Arguments))
        {
            errorMessage = "⚠️ Укажите команду: `/psh <команда>`\n\nПример: `/psh Get-Process`";
            return false;
        }

        errorMessage = null;
        return true;
    }

    protected override async Task RenderResponse(CommandContext ctx, ExecuteCommandResponse response)
    {
        if (response.Type != ResponseType.Text)
        {
            await base.RenderResponse(ctx, response);
            return;
        }

        await ctx.ReplyWithBack(WrapCode(response.Text ?? "Нет данных"));
    }

    private static string WrapCode(string text)
    {
        return text.Contains("```", StringComparison.Ordinal)
            ? text
            : $"```\n{text}\n```";
    }
}
