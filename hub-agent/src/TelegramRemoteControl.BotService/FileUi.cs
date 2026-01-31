using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Models;

namespace TelegramRemoteControl.BotService;

internal static class FileUi
{

    public static (string Text, InlineKeyboardMarkup Keyboard) BuildDrives(
        FileSessionManager.FileSession session,
        List<FileItem> items)
    {
        var rows = new List<List<InlineKeyboardButton>>();
        var row = new List<InlineKeyboardButton>();

        foreach (var drive in items)
        {
            var label = BuildDriveLabel(drive);
            var drivePath = NormalizeDrivePath(drive.Name);
            var id = session.CachePath(drivePath);
            row.Add(InlineKeyboardButton.WithCallbackData(label, $"f:d:{id}"));

            if (row.Count == 2)
            {
                rows.Add(row);
                row = new List<InlineKeyboardButton>();
            }
        }

        if (row.Count > 0)
            rows.Add(row);

        rows.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu")
        });

        var lines = items.Select(d =>
        {
            var free = d.Free.HasValue ? FormatSize(d.Free.Value) : "н/д";
            var total = d.Total.HasValue ? FormatSize(d.Total.Value) : "н/д";
            return $"`{d.Name}` - {free} / {total}";
        });

        var text = "📂 **Диски:**\n\n" + string.Join("\n", lines);
        return (text, new InlineKeyboardMarkup(rows));
    }

    public static (string Text, InlineKeyboardMarkup Keyboard) BuildDirectory(
        FileSessionManager.FileSession session,
        int pageSize)
    {
        var entries = session.Items
            .OrderByDescending(e => e.IsDirectory)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var size = pageSize > 0 ? pageSize : 8;
        var totalPages = Math.Max(1, (int)Math.Ceiling(entries.Count / (double)size));
        if (session.CurrentPage < 0) session.CurrentPage = 0;
        if (session.CurrentPage >= totalPages) session.CurrentPage = totalPages - 1;

        var pageEntries = entries
            .Skip(session.CurrentPage * size)
            .Take(size)
            .ToList();

        var buttons = new List<List<InlineKeyboardButton>>();

        foreach (var entry in pageEntries)
        {
            var fullPath = BuildFullPath(session.CurrentPath, entry.Name);
            var id = session.CachePath(fullPath);

            string label;
            string callback;

            if (entry.IsDirectory)
            {
                label = $"📁 {Truncate(entry.Name, 25)}";
                callback = $"f:n:{id}";
            }
            else
            {
                var sizeLabel = FormatSize(entry.Size);
                label = $"📄 {Truncate(entry.Name, 18)} ({sizeLabel})";
                callback = $"f:v:{id}";
            }

            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(label, callback)
            });
        }

        if (totalPages > 1)
        {
            var pageRow = new List<InlineKeyboardButton>();
            if (session.CurrentPage > 0)
                pageRow.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"f:pg:{session.CurrentPage - 1}"));
            pageRow.Add(InlineKeyboardButton.WithCallbackData(
                $"{session.CurrentPage + 1}/{totalPages}", $"f:pg:{session.CurrentPage}"));
            if (session.CurrentPage < totalPages - 1)
                pageRow.Add(InlineKeyboardButton.WithCallbackData("➡️", $"f:pg:{session.CurrentPage + 1}"));

            buttons.Add(pageRow);
        }

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("⬆️ Наверх", "f:up"),
            InlineKeyboardButton.WithCallbackData("💾 Диски", "f:root"),
            InlineKeyboardButton.WithCallbackData("🔄 Обновить", "f:refresh")
        });

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu")
        });

        var dirCount = entries.Count(e => e.IsDirectory);
        var fileCount = entries.Count(e => !e.IsDirectory);
        var totalSize = entries.Where(e => !e.IsDirectory).Sum(e => e.Size);

        var pathLabel = session.CurrentPath ?? "Компьютер";
        var text = $"📂 `{TruncatePath(pathLabel, 40)}`\n\n" +
                   $"📁 {dirCount} папок | 📄 {fileCount} файлов\n" +
                   $"💾 Всего: {FormatSize(totalSize)}";

        if (entries.Count == 0)
            text += "\n\n_(пусто)_";

        return (text, new InlineKeyboardMarkup(buttons));
    }

    public static string BuildPreviewText(string fileName, long size, string preview, string? langHint)
    {
        var safe = preview.Replace("```", "``\u200b`");
        var fence = string.IsNullOrWhiteSpace(langHint) ? "```" : $"```{langHint}";
        return $"📄 `{fileName}` ({FormatSize(size)})\n{fence}\n{safe}\n```";
    }

    public static InlineKeyboardMarkup BuildPreviewKeyboard(int fileId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📥 Скачать", $"f:dl:{fileId}"),
                InlineKeyboardButton.WithCallbackData("🗑 Закрыть", "f:close")
            }
        });
    }

    public static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return string.Format("{0:0.#} {1}", size, sizes[order]);
    }

    private static string BuildDriveLabel(FileItem drive)
    {
        var free = drive.Free.HasValue ? FormatSize(drive.Free.Value) : "н/д";
        return $"💾 {drive.Name} ({free})";
    }

    private static string NormalizeDrivePath(string name)
    {
        return name.EndsWith("\\", StringComparison.Ordinal) ? name : name + "\\";
    }

    private static string BuildFullPath(string? basePath, string name)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            return name;

        return Path.Combine(basePath, name);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 2)] + "..";

    private static string TruncatePath(string path, int maxLen)
    {
        if (path.Length <= maxLen) return path;
        return "..." + path[^Math.Max(1, maxLen - 3)..];
    }
}
