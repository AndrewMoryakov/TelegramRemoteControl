using System.Diagnostics;

namespace TelegramRemoteControl.Commands.Impl;

public class StatusCommand : CommandBase
{
    public override string Id => "status";
    public override string[] Aliases => new[] { "/status", "/st" };
    public override string Title => "Статус";
    public override string Icon => "📊";
    public override string Description => "Системная информация";
    public override string Category => Categories.Info;

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        var computerName = Environment.MachineName;
        var userName = Environment.UserName;
        var osVersion = Environment.OSVersion.ToString();
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var cpuCount = Environment.ProcessorCount;

        var process = Process.GetCurrentProcess();
        var workingSet = process.WorkingSet64 / 1024 / 1024;

        var status = $"""
            💻 *Статус системы*

            🖥 Компьютер: `{computerName}`
            👤 Пользователь: `{userName}`
            🪟 ОС: `{osVersion}`
            ⏱ Uptime: `{uptime:d\.hh\:mm\:ss}`
            🔢 CPU: `{cpuCount} ядер`
            📊 RAM бота: `{workingSet} MB`
            """;

        await ctx.ReplyWithBack(status);
    }
}
