using System.Runtime.InteropServices;

namespace TelegramRemoteControl.Commands.Impl;

public class SleepCommand : CommandBase, IConfirmableCommand
{
    public override string Id => "sleep";
    public override string[] Aliases => new[] { "/sleep" };
    public override string Title => "Сон";
    public override string Icon => "😴";
    public override string Description => "Режим сна";
    public override string Category => Categories.Control;

    public string ConfirmMessage => "😴 *Перевести компьютер в режим сна?*";

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.ReplyWithBack("😴 Переход в режим сна...");
        await Task.Delay(1000);
        SetSuspendState(false, false, false);
    }
}

public class HibernateCommand : CommandBase, IConfirmableCommand
{
    public override string Id => "hibernate";
    public override string[] Aliases => new[] { "/hibernate", "/hib" };
    public override string Title => "Гибернация";
    public override string Icon => "💤";
    public override string Description => "Гибернация";
    public override string Category => Categories.Control;

    public string ConfirmMessage => "💤 *Перевести компьютер в гибернацию?*";

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.ReplyWithBack("💤 Переход в гибернацию...");
        await Task.Delay(1000);
        SetSuspendState(true, false, false);
    }
}
