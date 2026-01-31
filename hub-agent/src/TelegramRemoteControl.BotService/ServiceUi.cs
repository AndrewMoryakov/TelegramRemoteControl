using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Models;

namespace TelegramRemoteControl.BotService;

internal static class ServiceUi
{
    public static string BuildListText(List<ServiceInfo> services)
    {
        var running = services
            .Where(s => string.Equals(s.Status, "Running", StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();

        var lines = running.Select((s, i) =>
            $"{i + 1,2}. {Truncate(s.DisplayName, 30)}");

        return $"""
            ⚙️ *Запущенные службы (топ-20):*
            ```
            {string.Join("\n", lines)}
            ```

            💡 Детали: `/svc <имя>`
            Примеры:
            `/svc wuauserv` — Windows Update
            `/svc spooler` — Печать
            """;
    }

    public static (string Text, InlineKeyboardMarkup Keyboard) BuildInfo(ServiceInfo svc)
    {
        var statusIcon = svc.Status switch
        {
            "Running" => "🟢",
            "Stopped" => "🔴",
            "Paused" => "🟡",
            _ => "⚪"
        };

        var text = $"""
            ⚙️ *Служба*

            📝 *Имя:* `{svc.Name}`
            📋 *Отображаемое:* `{EscapeMarkdown(svc.DisplayName)}`
            {statusIcon} *Статус:* `{svc.Status}`
            🔧 *Тип:* `{svc.StartType}`
            """;

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("▶️ Запустить", $"svc:start:{svc.Name}"),
                InlineKeyboardButton.WithCallbackData("⏹ Остановить", $"svc:stop:{svc.Name}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 Перезапустить", $"svc:restart:{svc.Name}"),
                InlineKeyboardButton.WithCallbackData("🔃 Обновить", $"svc:info:{svc.Name}")
            },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu") }
        });

        return (text, keyboard);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 2)] + "..";

    private static string EscapeMarkdown(string text) =>
        text.Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`").Replace("[", "\\[");
}
