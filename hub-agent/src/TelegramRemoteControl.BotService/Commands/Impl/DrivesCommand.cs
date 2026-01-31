using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class DrivesCommand : ProxyCommandBase
{
    public override string Id => "drives";
    public override string[] Aliases => new[] { "/drives" };
    public override string Title => "Диски";
    public override string? Icon => "💾";
    public override string? Description => "Список дисков";
    public override string Category => Categories.Info;

    protected override CommandType AgentCommandType => CommandType.Drives;
}
