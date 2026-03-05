using Microsoft.Extensions.Options;
using TelegramRemoteControl.BotService.Menu;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class UsersCommand : ICommand
{
    private readonly BotSettings _settings;

    public UsersCommand(IOptions<BotSettings> settings)
    {
        _settings = settings.Value;
    }

    public string Id => "users";
    public string[] Aliases => new[] { "/users" };
    public string Title => "Пользователи";
    public string? Icon => "👥";
    public string? Description => "Список пользователей и их статус";
    public string Category => Categories.Admin;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!IsAdmin(ctx.UserId))
        {
            await ctx.ReplyWithBack("⛔ Нет доступа");
            return;
        }

        var users = await ctx.Hub.GetAllUsers();
        if (users.Count == 0)
        {
            await ctx.ReplyWithBack("👥 Нет пользователей");
            return;
        }

        var authorized = users.Where(u => u.IsAuthorized).ToList();
        var pending = users.Where(u => !u.IsAuthorized).ToList();

        var lines = new List<string>
        {
            $"👥 *Пользователи* ({authorized.Count} активных, {pending.Count} ожидают)",
            ""
        };

        if (authorized.Count > 0)
        {
            lines.Add("*✅ Авторизованы:*");
            foreach (var u in authorized)
                lines.Add($"  • `{u.UserId}` {FormatName(u)} — был {FormatAgo(u.LastSeen)}");
            lines.Add("");
        }

        if (pending.Count > 0)
        {
            lines.Add("*⏳ Ожидают одобрения:*");
            foreach (var u in pending)
                lines.Add($"  • `{u.UserId}` {FormatName(u)} — `/approve {u.UserId}`");
        }

        await ctx.ReplyWithBack(string.Join('\n', lines));
    }

    private static string FormatName(Shared.Contracts.HubApi.UserDto u)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(u.FirstName)) parts.Add(u.FirstName);
        if (!string.IsNullOrWhiteSpace(u.Username)) parts.Add($"@{u.Username}");
        return parts.Count > 0 ? string.Join(" ", parts) : "—";
    }

    private static string FormatAgo(DateTime dt)
    {
        var ago = DateTime.UtcNow - dt;
        if (ago.TotalMinutes < 2) return "только что";
        if (ago.TotalHours < 1) return $"{(int)ago.TotalMinutes}м назад";
        if (ago.TotalDays < 1) return $"{(int)ago.TotalHours}ч назад";
        return $"{(int)ago.TotalDays}д назад";
    }

    private bool IsAdmin(long userId) =>
        _settings.AuthorizedUsers.Length > 0 && _settings.AuthorizedUsers.Contains(userId);
}
