using System.Diagnostics;

namespace TelegramRemoteControl.Commands.Impl;

public class RestartCommand : CommandBase, IConfirmableCommand
{
    public override string Id => "restart";
    public override string[] Aliases => new[] { "/restart", "/reboot" };
    public override string Title => "Рестарт";
    public override string Icon => "🔄";
    public override string Description => "Перезагрузить компьютер";
    public override string Category => Categories.Control;

    public string ConfirmMessage => "🔄 *Перезагрузить компьютер?*\n\nКомпьютер перезагрузится через 10 секунд.";

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        Process.Start("shutdown", "/r /t 10");
        await ctx.ReplyWithBack("🔄 Перезагрузка через 10 секунд...\n\nОтмена: `/cmd shutdown /a`");
    }
}
