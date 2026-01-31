using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class SleepCommand : ConfirmableProxyCommandBase
{
    public override string Id => "sleep";
    public override string[] Aliases => new[] { "/sleep" };
    public override string Title => "Сон";
    public override string? Icon => "😴";
    public override string? Description => "Режим сна";
    public override string Category => Categories.Control;

    protected override CommandType AgentCommandType => CommandType.Sleep;

    public override string ConfirmMessage => "😴 *Перевести компьютер в режим сна?*";
}
