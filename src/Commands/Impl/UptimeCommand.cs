using System.Management;

namespace TelegramRemoteControl.Commands.Impl;

public class UptimeCommand : CommandBase
{
    public override string Id => "uptime";
    public override string[] Aliases => new[] { "/uptime" };
    public override string Title => "Uptime";
    public override string Icon => "⏱";
    public override string Description => "Время работы системы";
    public override string Category => Categories.Info;

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var bootTime = DateTime.Now - uptime;

        // Получаем время последнего выключения из Event Log
        string lastShutdown = "н/д";
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                var lastBootStr = obj["LastBootUpTime"]?.ToString();
                if (!string.IsNullOrEmpty(lastBootStr))
                {
                    var lastBoot = ManagementDateTimeConverter.ToDateTime(lastBootStr);
                    bootTime = lastBoot;
                }
            }
        }
        catch { }

        var text = $"""
            ⏱ *Время работы системы*

            🟢 *Uptime:* `{uptime.Days} дн. {uptime.Hours} ч. {uptime.Minutes} мин.`
            🔄 *Запуск:* `{bootTime:dd.MM.yyyy HH:mm:ss}`
            📅 *Сейчас:* `{DateTime.Now:dd.MM.yyyy HH:mm:ss}`
            """;

        await ctx.ReplyWithBack(text);
    }
}
