using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class HibernateCommand : ConfirmableProxyCommandBase
{
    public override string Id => "hibernate";
    public override string[] Aliases => new[] { "/hibernate", "/hib" };
    public override string Title => "Гибернация";
    public override string? Icon => "💤";
    public override string? Description => "Гибернация";
    public override string Category => Categories.Control;

    protected override CommandType AgentCommandType => CommandType.Hibernate;

    public override string ConfirmMessage => "💤 *Перевести компьютер в гибернацию?*";
}
