using System.Diagnostics;

namespace TelegramRemoteControl.Commands.Impl;

public class KillCommand : CommandBase
{
    public override string Id => "kill";
    public override string[] Aliases => new[] { "/kill" };
    public override string Title => "Завершить";
    public override string Icon => "💀";
    public override string Description => "Завершить процесс";
    public override string Category => Categories.Control;

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Arguments))
        {
            await ctx.ReplyWithBack("⚠️ Укажите имя или PID процесса:\n`/kill <имя/pid>`\n\nПример: `/kill notepad`");
            return;
        }

        int killed = 0;
        var target = ctx.Arguments.Trim();

        if (int.TryParse(target, out int pid))
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                proc.Kill();
                killed = 1;
            }
            catch { }
        }
        else
        {
            var processes = Process.GetProcessesByName(target);
            foreach (var proc in processes)
            {
                try { proc.Kill(); killed++; } catch { }
            }
        }

        var message = killed > 0
            ? $"💀 Завершено процессов: {killed}"
            : "❌ Процесс не найден";

        await ctx.ReplyWithBack(message);
    }
}
