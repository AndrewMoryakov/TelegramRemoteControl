using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class RestartCommand : ConfirmableProxyCommandBase
{
    public override string Id => "restart";
    public override string[] Aliases => new[] { "/restart", "/reboot" };
    public override string Title => "Рестарт";
    public override string? Icon => "🔄";
    public override string? Description => "Перезагрузить компьютер";
    public override string Category => Categories.Control;

    protected override CommandType AgentCommandType => CommandType.Restart;

    public override string ConfirmMessage =>
        "🔄 *Перезагрузить компьютер?*\n\nКомпьютер перезагрузится через 10 секунд.";
}
