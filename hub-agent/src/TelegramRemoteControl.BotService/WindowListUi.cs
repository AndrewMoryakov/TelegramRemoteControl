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
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📸 Скриншот", $"win:ss:{win.Hwnd}"),
                InlineKeyboardButton.WithCallbackData("🎯 Фокус", $"win:focus:{win.Hwnd}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("➖ Свернуть", $"win:min:{win.Hwnd}"),
                InlineKeyboardButton.WithCallbackData("➕ Развернуть", $"win:max:{win.Hwnd}"),
                InlineKeyboardButton.WithCallbackData("🔄 Восстановить", $"win:restore:{win.Hwnd}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⌨️ Ввести текст", $"win:type:{win.Hwnd}"),
                InlineKeyboardButton.WithCallbackData("🎹 Клавиши", $"win:keys:{win.Hwnd}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("❌ Закрыть", $"win:close:{win.Hwnd}")
            },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ К списку", "win:list") }
        });

        return (text, keyboard);
    }

    public static InlineKeyboardMarkup BuildKeys(long hwnd)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⏎ Enter",    $"win:key:{hwnd}:Enter"),
                InlineKeyboardButton.WithCallbackData("⎋ Esc",      $"win:key:{hwnd}:Escape"),
                InlineKeyboardButton.WithCallbackData("⌫ Backspace",$"win:key:{hwnd}:Backspace"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🗑 Delete",  $"win:key:{hwnd}:Delete"),
                InlineKeyboardButton.WithCallbackData("⇥ Tab",      $"win:key:{hwnd}:Tab"),
                InlineKeyboardButton.WithCallbackData("␣ Space",    $"win:key:{hwnd}:Space"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("↑", $"win:key:{hwnd}:Up"),
                InlineKeyboardButton.WithCallbackData("↓", $"win:key:{hwnd}:Down"),
                InlineKeyboardButton.WithCallbackData("←", $"win:key:{hwnd}:Left"),
                InlineKeyboardButton.WithCallbackData("→", $"win:key:{hwnd}:Right"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Home",  $"win:key:{hwnd}:Home"),
                InlineKeyboardButton.WithCallbackData("End",   $"win:key:{hwnd}:End"),
                InlineKeyboardButton.WithCallbackData("PgUp",  $"win:key:{hwnd}:PageUp"),
                InlineKeyboardButton.WithCallbackData("PgDn",  $"win:key:{hwnd}:PageDown"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("F5 🔄", $"win:key:{hwnd}:F5"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("◀️ К окну", $"win:info:{hwnd}"),
            }
        });
    }

    private static string EscapeMarkdown(string text) =>
        text.Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`").Replace("[", "\\[");
}
