using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Models;

namespace TelegramRemoteControl.BotService;

internal static class ProcessListUi
{
    public const int MaxListCount = 20;

    public static List<ProcessInfo> SortForList(List<ProcessInfo> items)
    {
        return items
            .OrderByDescending(p => p.Ram)
            .ThenByDescending(p => p.Cpu)
            .ToList();
    }

    public static string BuildList(List<ProcessInfo> items)
    {
        var list = SortForList(items).Take(MaxListCount).ToList();

        var lines = list.Select((p, i) =>
            string.Format("{0,2}. {1,-22} {2,5} MB  {3,5:F1}%",
                i + 1,
                Truncate(p.Name, 22),
                p.Ram,
                p.Cpu));

        return "📋 Процессы (топ 20)\n" +
               "```\n" +
               "№  Имя                      RAM    CPU\n" +
               string.Join("\n", lines) +
               "\n```\n\n" +
               "💡 Выбрать: `/proc <номер>`";
    }

    public static (string Text, InlineKeyboardMarkup Keyboard)? BuildInfo(List<ProcessInfo> items, int index)
    {
        if (index < 1 || index > items.Count)
            return null;

        var item = items[index - 1];

        var text = $"""
            📋 *Процесс #{index}*

            📝 Имя: `{item.Name}`
            🔢 PID: `{item.Pid}`
            💾 RAM: `{item.Ram} MB`
            ⚡ CPU: `{item.Cpu:F1}%`

            Назад: `/processes`
            """;

        var rows = new List<InlineKeyboardButton[]>
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("💀 Завершить", $"proc:kill:{item.Pid}"),
                InlineKeyboardButton.WithCallbackData("🔄 Обновить", $"proc:info:{item.Pid}")
            }
        };

        return (text, new InlineKeyboardMarkup(rows));
    }

    public static (string Text, InlineKeyboardMarkup Keyboard)? BuildInfoByPid(List<ProcessInfo> items, int pid)
    {
        var item = items.FirstOrDefault(p => p.Pid == pid);
        if (item == null)
            return null;

        var text = $"""
            📋 *Процесс*

            📝 Имя: `{item.Name}`
            🔢 PID: `{item.Pid}`
            💾 RAM: `{item.Ram} MB`
            ⚡ CPU: `{item.Cpu:F1}%`

            Назад: `/processes`
            """;

        var rows = new List<InlineKeyboardButton[]>
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("💀 Завершить", $"proc:kill:{item.Pid}"),
                InlineKeyboardButton.WithCallbackData("🔄 Обновить", $"proc:info:{item.Pid}")
            }
        };

        return (text, new InlineKeyboardMarkup(rows));
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 2)] + "..";
}
