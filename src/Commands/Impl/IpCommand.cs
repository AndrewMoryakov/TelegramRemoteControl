namespace TelegramRemoteControl.Commands.Impl;

public class IpCommand : CommandBase
{
    public override string Id => "ip";
    public override string[] Aliases => new[] { "/ip" };
    public override string Title => "IP";
    public override string Icon => "🌐";
    public override string Description => "IP адреса компьютера";
    public override string Category => Categories.Info;

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        var result = await RunShellAsync("powershell.exe",
            "-NoProfile -Command \"Get-NetIPAddress | Where-Object { $_.AddressFamily -eq 'IPv4' -and $_.IPAddress -ne '127.0.0.1' } | Select-Object IPAddress, InterfaceAlias | Format-Table -AutoSize\"",
            ctx.CancellationToken);

        await ctx.ReplyWithBack($"🌐 *IP адреса:*\n```\n{result}\n```");
    }
}
