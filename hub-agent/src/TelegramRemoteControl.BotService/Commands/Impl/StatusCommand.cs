using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class StatusCommand : ProxyCommandBase
{
    public override string Id => "status";
    public override string[] Aliases => new[] { "/status" };
    public override string Title => "Статус";
    public override string? Icon => "📊";
    public override string? Description => "Информация о системе";
    public override string Category => Categories.Info;
    protected override CommandType AgentCommandType => CommandType.Status;
}
