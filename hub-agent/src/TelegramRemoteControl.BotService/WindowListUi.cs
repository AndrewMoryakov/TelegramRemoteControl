using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Models;

namespace TelegramRemoteControl.BotService;

internal static class WindowListUi
{
    public static (string Text, InlineKeyboardMarkup Keyboard) BuildList(List<WindowInfo> windows)
    {
        var lines = windows.Select((w, i) =>
        {
            var stateIcon = w.State switch
            {
                "minimized" => "➖",
                "maximized" => "➕",
                _ => "🔲"
            };
            var title = w.Title.Length > 40 ? w.Title[..37] + "..." : w.Title;
            return $"{i + 1}. {stateIcon} {title}";
        });

        var text = "🪟 Окна\n" +
                   "```\n" +
                   string.Join("\n", lines) +
                   "\n```\n" +
                   $"\nВсего: {windows.Count}";

        var rows = new List<InlineKeyboardButton[]>();
        for (int i = 0; i < Math.Min(windows.Count, 10); i++)
        {
            var w = windows[i];
            var label = w.Title.Length > 20 ? w.Title[..17] + "..." : w.Title;
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData($"{i + 1}. {label}", $"win:info:{w.Hwnd}")
            });
        }

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("➖ Свернуть все", "win:minall"),
            InlineKeyboardButton.WithCallbackData("🔄 Обновить", "win:list")
        });
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu") });

        return (text, new InlineKeyboardMarkup(rows));
    }

    public static (string Text, InlineKeyboardMarkup Keyboard)? BuildInfo(WindowInfo win)
    {
        var stateIcon = win.State switch
        {
            "minimized" => "➖",
            "maximized" => "➕",
            _ => "🔲"
        };

        var text = $"""
            🪟 *Окно*

            📝 Заголовок: `{EscapeMarkdown(win.Title)}`
            🔢 HWND: `{win.Hwnd}`
            🔢 PID: `{win.Pid}`
            {stateIcon} Состояние: `{win.State}`
            """;

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📸 Скриншот", $"win:ss:{win.Hwnd}") },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("➖ Свернуть", $"win:min:{win.Hwnd}"),
                InlineKeyboardButton.WithCallbackData("➕ Развернуть", $"win:max:{win.Hwnd}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 Восстановить", $"win:restore:{win.Hwnd}"),
                InlineKeyboardButton.WithCallbackData("❌ Закрыть", $"win:close:{win.Hwnd}")
            },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ К списку", "win:list") }
        });

        return (text, keyboard);
    }

    private static string EscapeMarkdown(string text) =>
        text.Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`").Replace("[", "\\[");
}
