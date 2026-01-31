using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class ShutdownCommand : ConfirmableProxyCommandBase
{
    public override string Id => "shutdown";
    public override string[] Aliases => new[] { "/shutdown" };
    public override string Title => "Выключить";
    public override string? Icon => "🔴";
    public override string? Description => "Выключить компьютер";
    public override string Category => Categories.Control;

    protected override CommandType AgentCommandType => CommandType.Shutdown;

    public override string ConfirmMessage =>
        "🔴 *Выключить компьютер?*\n\nКомпьютер выключится через 10 секунд.";
}
