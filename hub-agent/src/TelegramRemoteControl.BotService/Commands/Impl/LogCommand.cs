using Microsoft.Extensions.Options;
using TelegramRemoteControl.BotService.Menu;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class LogCommand : ICommand
{
    private readonly BotSettings _settings;

    public LogCommand(IOptions<BotSettings> settings)
    {
        _settings = settings.Value;
    }

    public string Id => "log";
    public string[] Aliases => new[] { "/log" };
    public string Title => "Лог команд";
    public string? Icon => "📜";
    public string? Description => "Последние выполненные команды";
    public string Category => Categories.Admin;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!IsAdmin(ctx.UserId))
        {
            await ctx.ReplyWithBack("⛔ Нет доступа");
            return;
        }

        int limit = 20;
        if (int.TryParse(ctx.Arguments?.Trim(), out var parsed) && parsed > 0)
            limit = Math.Min(parsed, 50);

        var entries = await ctx.Hub.GetRecentCommands(limit);
        if (entries.Count == 0)
        {
            await ctx.ReplyWithBack("📜 Лог пуст");
            return;
        }

        var lines = new List<string> { $"📜 *Последние {entries.Count} команд:*", "" };
        foreach (var e in entries)
        {
            var status = e.Success ? "✅" : "❌";
            var time = e.Timestamp.ToLocalTime().ToString("MM/dd HH:mm");
            var agentShort = e.AgentId.Length > 8 ? e.AgentId[..8] : e.AgentId;
            lines.Add($"{status} `{time}` *{e.CommandType}* [{agentShort}] {e.DurationMs}ms");
        }

        await ctx.ReplyWithBack(string.Join('\n', lines));
    }

    private bool IsAdmin(long userId) =>
        _settings.AuthorizedUsers.Length > 0 && _settings.AuthorizedUsers.Contains(userId);
}
