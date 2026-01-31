using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class UptimeCommand : ProxyCommandBase
{
    public override string Id => "uptime";
    public override string[] Aliases => new[] { "/uptime" };
    public override string Title => "Uptime";
    public override string? Icon => "⏱";
    public override string? Description => "Время работы системы";
    public override string Category => Categories.Info;

    protected override CommandType AgentCommandType => CommandType.Uptime;
}
