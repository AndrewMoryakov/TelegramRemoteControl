using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class MonitorCommand : ProxyCommandBase
{
    public override string Id => "monitor";
    public override string[] Aliases => new[] { "/monitor" };
    public override string Title => "Монитор";
    public override string? Icon => "🖥";
    public override string? Description => "Информация о дисплее";
    public override string Category => Categories.Info;

    protected override CommandType AgentCommandType => CommandType.Monitor;
}
