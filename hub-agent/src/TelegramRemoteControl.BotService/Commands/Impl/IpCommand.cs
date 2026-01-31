using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class IpCommand : ProxyCommandBase
{
    public override string Id => "ip";
    public override string[] Aliases => new[] { "/ip" };
    public override string Title => "IP";
    public override string? Icon => "🌐";
    public override string? Description => "IP адреса";
    public override string Category => Categories.Info;

    protected override CommandType AgentCommandType => CommandType.Ip;
}
