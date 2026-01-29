namespace TelegramRemoteControl.Commands.Impl;

public class CmdCommand : CommandBase
{
    public override string Id => "cmd";
    public override string[] Aliases => new[] { "/cmd" };
    public override string Title => "CMD";
    public override string Icon => "⚡";
    public override string Description => "Выполнить команду CMD";
    public override string Category => Categories.Shell;

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Arguments))
        {
            await ctx.ReplyWithBack("⚠️ Укажите команду: `/cmd <команда>`\n\nПример: `/cmd dir`");
            return;
        }

        await SendAsync(ctx, $"⚡ Выполняю: `{ctx.Arguments}`");

        var result = await RunShellAsync("cmd.exe", $"/c {ctx.Arguments}", ctx.CancellationToken);
        await SendLongAsync(ctx, result);
    }
}
