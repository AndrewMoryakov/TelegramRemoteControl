using System.Runtime.InteropServices;

namespace TelegramRemoteControl.Commands.Impl;

public class LockCommand : CommandBase
{
    public override string Id => "lock";
    public override string[] Aliases => new[] { "/lock" };
    public override string Title => "Блокировка";
    public override string Icon => "🔒";
    public override string Description => "Заблокировать экран";
    public override string Category => Categories.Control;

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        LockWorkStation();
        await ctx.ReplyWithBack("🔒 Экран заблокирован");
    }
}
