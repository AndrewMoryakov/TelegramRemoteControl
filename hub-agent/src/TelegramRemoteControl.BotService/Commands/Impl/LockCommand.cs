using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class LockCommand : ProxyCommandBase
{
    public override string Id => "lock";
    public override string[] Aliases => new[] { "/lock" };
    public override string Title => "Блокировка";
    public override string? Icon => "🔒";
    public override string? Description => "Заблокировать экран";
    public override string Category => Categories.Control;

    protected override CommandType AgentCommandType => CommandType.Lock;
}
