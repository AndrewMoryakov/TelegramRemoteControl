using Microsoft.Extensions.Options;
using TelegramRemoteControl.BotService.Menu;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class SysHealthCommand : ICommand
{
    private readonly BotSettings _settings;

    public SysHealthCommand(IOptions<BotSettings> settings)
    {
        _settings = settings.Value;
    }

    public string Id => "syshealth";
    public string[] Aliases => new[] { "/syshealth" };
    public string Title => "Здоровье системы";
    public string? Icon => "🏥";
    public string? Description => "Состояние Hub, агентов, БД";
    public string Category => Categories.Admin;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!IsAdmin(ctx.UserId))
        {
            await ctx.ReplyWithBack("⛔ Нет доступа");
            return;
        }

        var health = await ctx.Hub.GetSystemHealth();
        if (health == null)
        {
            await ctx.ReplyWithBack("❌ Hub недоступен");
            return;
        }

        var uptime = DateTime.UtcNow - health.StartedAt;
        var uptimeStr = uptime.TotalHours >= 1
            ? $"{(int)uptime.TotalHours}ч {uptime.Minutes}м"
            : $"{uptime.Minutes}м {uptime.Seconds}с";

        var dbKb = health.DbSizeBytes / 1024.0;
        var dbStr = dbKb >= 1024 ? $"{dbKb / 1024:F1} MB" : $"{dbKb:F0} KB";

        var lines = new List<string>
        {
            "🏥 *Состояние системы*",
            "",
            $"⏱ Hub запущен: {uptimeStr} назад",
            $"🖥 Агенты: {health.OnlineAgents} онлайн / {health.TotalAgents} всего",
            $"📋 Команд сегодня: {health.CommandsToday}",
            $"🗄 БД: {dbStr}",
        };

        var recent = await ctx.Hub.GetRecentCommands(10);
        if (recent.Count > 0)
        {
            lines.Add("");
            lines.Add("*Последние команды:*");
            foreach (var e in recent)
            {
                var status = e.Success ? "✅" : "❌";
                var time = e.Timestamp.ToLocalTime().ToString("HH:mm");
                lines.Add($"  {status} {time} `{e.CommandType}` — {e.DurationMs}ms");
            }
        }

        await ctx.ReplyWithBack(string.Join('\n', lines));
    }

    private bool IsAdmin(long userId) =>
        _settings.AuthorizedUsers.Length > 0 && _settings.AuthorizedUsers.Contains(userId);
}
