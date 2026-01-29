namespace TelegramRemoteControl.Commands.Impl;

public class PowerShellCommand : CommandBase
{
    public override string Id => "powershell";
    public override string[] Aliases => new[] { "/powershell", "/psh" };
    public override string Title => "PowerShell";
    public override string Icon => "💠";
    public override string Description => "Выполнить команду PowerShell";
    public override string Category => Categories.Shell;

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Arguments))
        {
            await ctx.ReplyWithBack("⚠️ Укажите команду: `/psh <команда>`\n\nПример: `/psh Get-Process`");
            return;
        }

        await SendAsync(ctx, $"💠 Выполняю PS: `{ctx.Arguments}`");

        var result = await RunShellAsync("powershell.exe", $"-NoProfile -Command {ctx.Arguments}", ctx.CancellationToken);
        await SendLongAsync(ctx, result);
    }
}
