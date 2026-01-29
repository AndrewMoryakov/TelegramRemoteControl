using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System.IO.Compression;

namespace TelegramRemoteControl.Commands.Impl;

public class FilesCommand : CommandBase
{
    public override string Id => "files";
    public override string[] Aliases => new[] { "/files", "/ls", "/dir" };
    public override string Title => "Файлы";
    public override string Icon => "📁";
    public override string Description => "Файловый менеджер";
    public override string Category => Categories.Info;

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        // При вызове команды показываем все диски
        await ShowDrivesAsync(ctx);
    }

    private async Task ShowDrivesAsync(CommandContext ctx)
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .ToList();

        var buttons = new List<List<InlineKeyboardButton>>();

        // Кнопки дисков (по 3 в ряд)
        var row = new List<InlineKeyboardButton>();
        foreach (var drive in drives)
        {
            var icon = drive.DriveType switch
            {
                DriveType.Fixed => "💾",
                DriveType.Removable => "💿",
                DriveType.Network => "🌐",
                DriveType.CDRom => "📀",
                _ => "📁"
            };

            var label = string.Format("{0} {1}", icon, drive.Name.TrimEnd('\\'));
            var freeSpace = FormatSize(drive.AvailableFreeSpace);
            var totalSpace = FormatSize(drive.TotalSize);

            row.Add(InlineKeyboardButton.WithCallbackData(
                string.Format("{0} ({1})", label, freeSpace),
                string.Format("f:d:{0}", drive.Name.TrimEnd('\\'))));

            if (row.Count == 3)
            {
                buttons.Add(row);
                row = new List<InlineKeyboardButton>();
            }
        }
        if (row.Count > 0) buttons.Add(row);

        // Кнопка меню
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu")
        });

        var text = "📂 **Выберите диск:**\n\n";
        foreach (var drive in drives)
        {
            var free = FormatSize(drive.AvailableFreeSpace);
            var total = FormatSize(drive.TotalSize);
            text += string.Format("**{0}** - {1} / {2}\n", drive.Name.TrimEnd('\\'), free, total);
        }

        await ctx.Bot.SendMessage(ctx.ChatId, text,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            replyMarkup: new InlineKeyboardMarkup(buttons),
            cancellationToken: ctx.CancellationToken);
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
}
