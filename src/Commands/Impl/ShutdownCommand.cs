using System.Diagnostics;

namespace TelegramRemoteControl.Commands.Impl;

public class ShutdownCommand : CommandBase, IConfirmableCommand
{
    public override string Id => "shutdown";
    public override string[] Aliases => new[] { "/shutdown" };
    public override string Title => "Выключить";
    public override string Icon => "🔴";
    public override string Description => "Выключить компьютер";
    public override string Category => Categories.Control;

    public string ConfirmMessage => "🔴 *Выключить компьютер?*\n\nКомпьютер выключится через 10 секунд.";

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        Process.Start("shutdown", "/s /t 10");
        await ctx.ReplyWithBack("🔴 Выключение через 10 секунд...\n\nОтмена: `/cmd shutdown /a`");
    }
}
