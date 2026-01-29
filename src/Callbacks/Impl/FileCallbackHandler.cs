using System.IO.Compression;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.Commands.Impl;
using TelegramRemoteControl.Helpers;
using TelegramRemoteControl.Menu;
using File = System.IO.File;

namespace TelegramRemoteControl.Callbacks.Impl;

/// <summary>
/// Обработчик callback для файлового менеджера (f:*)
/// Навигация полностью на кнопках
/// </summary>
public class FileCallbackHandler : ICallbackHandler
{
    public string Prefix => "f";

    private readonly MenuBuilder _menu;

    // Сессии пользователей: chatId -> FileSession
    private static readonly Dictionary<long, FileSession> _sessions = new();

    public FileCallbackHandler(MenuBuilder menu)
    {
        _menu = menu;
    }

    public async Task HandleAsync(CallbackContext ctx)
    {
        if (ctx.Args.Length < 1)
        {
            await ctx.AnswerAsync("Неверный формат");
            return;
        }

        var session = GetSession(ctx.ChatId);
        var action = ctx.Args[0];

        try
        {
            switch (action)
            {
                case "d": // Выбор диска: f:d:C
                    if (ctx.Args.Length >= 2)
                    {
                        var drive = ctx.Args[1] + ":\\";
                        session.CurrentPath = drive;
                        await ShowDirectoryAsync(ctx, session);
                    }
                    break;

                case "n": // Навигация в папку: f:n:ID
                    if (ctx.Args.Length >= 2 && int.TryParse(ctx.Args[1], out var navId))
                    {
                        var path = session.GetPath(navId);
                        if (path != null && Directory.Exists(path))
                        {
                            session.CurrentPath = path;
                            await ShowDirectoryAsync(ctx, session);
                        }
                        else
                        {
                            await ctx.AnswerAsync("Папка не найдена", showAlert: true);
                        }
                    }
                    break;

                case "v": // Просмотр/превью файла: f:v:ID
                    if (ctx.Args.Length >= 2 && int.TryParse(ctx.Args[1], out var viewId))
                    {
                        var path = session.GetPath(viewId);
                        if (path != null && File.Exists(path))
                        {
                            await ctx.AnswerAsync();
                            await SendPreviewAsync(ctx, session, path, viewId);
                        }
                        else
                        {
                            await ctx.AnswerAsync("Файл не найден", showAlert: true);
                        }
                    }
                    break;

                case "dl": // Скачать файл: f:dl:ID
                    if (ctx.Args.Length >= 2 && int.TryParse(ctx.Args[1], out var dlId))
                    {
                        var path = session.GetPath(dlId);
                        if (path != null && File.Exists(path))
                        {
                            await ctx.AnswerAsync("Отправка файла...");
                            await SendFileAsync(ctx, path);
                        }
                        else
                        {
                            await ctx.AnswerAsync("Файл не найден", showAlert: true);
                        }
                    }
                    break;

                case "close": // Удалить превью-сообщение
                    await ctx.AnswerAsync();
                    try
                    {
                        await ctx.Bot.DeleteMessage(ctx.ChatId, ctx.MessageId, ctx.CancellationToken);
                    }
                    catch { }
                    break;

                case "up": // Наверх
                    var parent = Directory.GetParent(session.CurrentPath);
                    if (parent != null)
                    {
                        session.CurrentPath = parent.FullName;
                        await ShowDirectoryAsync(ctx, session);
                    }
                    else
                    {
                        // Показать диски
                        await ShowDrivesAsync(ctx);
                    }
                    break;

                case "zip": // Скачать ZIP
                    await ctx.AnswerAsync("Создание архива...");
                    await SendZipAsync(ctx, session.CurrentPath);
                    break;

                case "root": // Показать диски
                    await ShowDrivesAsync(ctx);
                    break;

                case "pg": // Страница: f:pg:NUM
                    if (ctx.Args.Length >= 2 && int.TryParse(ctx.Args[1], out var page))
                    {
                        session.CurrentPage = page;
                        await ShowDirectoryAsync(ctx, session);
                    }
                    break;

                default:
                    await ctx.AnswerAsync("Неизвестное действие");
                    break;
            }
        }
        catch (UnauthorizedAccessException)
        {
            await ctx.AnswerAsync("Нет доступа", showAlert: true);
        }
        catch (Exception ex)
        {
            await ctx.AnswerAsync(string.Format("Ошибка: {0}", ex.Message), showAlert: true);
        }
    }

    private FileSession GetSession(long chatId)
    {
        if (!_sessions.TryGetValue(chatId, out var session))
        {
            session = new FileSession();
            _sessions[chatId] = session;
        }
        return session;
    }

    private async Task ShowDrivesAsync(CallbackContext ctx)
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .ToList();

        var buttons = new List<List<InlineKeyboardButton>>();
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

            var free = FilesCommand.FormatSize(drive.AvailableFreeSpace);
            row.Add(InlineKeyboardButton.WithCallbackData(
                string.Format("{0} {1} ({2})", icon, drive.Name.TrimEnd('\\'), free),
                string.Format("f:d:{0}", drive.Name.TrimEnd('\\'))));

            if (row.Count == 2)
            {
                buttons.Add(row);
                row = new List<InlineKeyboardButton>();
            }
        }
        if (row.Count > 0) buttons.Add(row);

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu")
        });

        var text = "📂 **Диски:**\n\n";
        foreach (var drive in drives)
        {
            text += string.Format("`{0}` - {1} / {2}\n",
                drive.Name.TrimEnd('\\'),
                FilesCommand.FormatSize(drive.AvailableFreeSpace),
                FilesCommand.FormatSize(drive.TotalSize));
        }

        await ctx.Bot.EditMessageText(
            ctx.ChatId,
            ctx.MessageId,
            text,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            replyMarkup: new InlineKeyboardMarkup(buttons),
            cancellationToken: ctx.CancellationToken);
    }

    private async Task ShowDirectoryAsync(CallbackContext ctx, FileSession session)
    {
        var path = session.CurrentPath;
        session.ClearCache();

        var entries = new List<FileSystemEntry>();

        // Папки
        try
        {
            foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
            {
                var info = new DirectoryInfo(dir);
                entries.Add(new FileSystemEntry
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    IsDirectory = true
                });
            }
        }
        catch { }

        // Файлы
        try
        {
            foreach (var file in Directory.GetFiles(path).OrderBy(f => f))
            {
                var info = new FileInfo(file);
                entries.Add(new FileSystemEntry
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    IsDirectory = false,
                    Size = info.Length
                });
            }
        }
        catch { }

        // Пагинация
        const int pageSize = 8;
        var totalPages = (int)Math.Ceiling(entries.Count / (double)pageSize);
        if (session.CurrentPage >= totalPages) session.CurrentPage = 0;
        if (session.CurrentPage < 0) session.CurrentPage = 0;

        var pageEntries = entries
            .Skip(session.CurrentPage * pageSize)
            .Take(pageSize)
            .ToList();

        var buttons = new List<List<InlineKeyboardButton>>();

        // Кнопки файлов/папок
        foreach (var entry in pageEntries)
        {
            var id = session.CachePath(entry.FullPath);
            string label;
            string callback;

            if (entry.IsDirectory)
            {
                label = string.Format("📁 {0}", TruncateName(entry.Name, 25));
                callback = string.Format("f:n:{0}", id);
            }
            else
            {
                var ext = Path.GetExtension(entry.Name);
                var typeInfo = FileTypeRegistry.GetInfo(ext);
                var size = FilesCommand.FormatSize(entry.Size);
                label = string.Format("{0} {1} ({2})", typeInfo.Icon, TruncateName(entry.Name, 18), size);
                callback = string.Format("f:v:{0}", id);
            }

            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(label, callback)
            });
        }

        // Пагинация
        if (totalPages > 1)
        {
            var pageRow = new List<InlineKeyboardButton>();
            if (session.CurrentPage > 0)
            {
                pageRow.Add(InlineKeyboardButton.WithCallbackData("⬅️", string.Format("f:pg:{0}", session.CurrentPage - 1)));
            }
            pageRow.Add(InlineKeyboardButton.WithCallbackData(
                string.Format("{0}/{1}", session.CurrentPage + 1, totalPages), "f:pg:" + session.CurrentPage));
            if (session.CurrentPage < totalPages - 1)
            {
                pageRow.Add(InlineKeyboardButton.WithCallbackData("➡️", string.Format("f:pg:{0}", session.CurrentPage + 1)));
            }
            buttons.Add(pageRow);
        }

        // Навигация
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("⬆️ Наверх", "f:up"),
            InlineKeyboardButton.WithCallbackData("📥 ZIP", "f:zip"),
            InlineKeyboardButton.WithCallbackData("💾 Диски", "f:root")
        });

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu")
        });

        // Статистика
        var dirCount = entries.Count(e => e.IsDirectory);
        var fileCount = entries.Count(e => !e.IsDirectory);
        var totalSize = entries.Where(e => !e.IsDirectory).Sum(e => e.Size);

        var text = string.Format("📂 `{0}`\n\n📁 {1} папок | 📄 {2} файлов\n💾 Всего: {3}",
            TruncatePath(path, 40),
            dirCount,
            fileCount,
            FilesCommand.FormatSize(totalSize));

        if (entries.Count == 0)
        {
            text += "\n\n_(пусто)_";
        }

        await ctx.Bot.EditMessageText(
            ctx.ChatId,
            ctx.MessageId,
            text,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            replyMarkup: new InlineKeyboardMarkup(buttons),
            cancellationToken: ctx.CancellationToken);
    }

    private async Task SendPreviewAsync(CallbackContext ctx, FileSession session, string filePath, int fileId)
    {
        var fileInfo = new FileInfo(filePath);
        var ext = Path.GetExtension(filePath);
        var typeInfo = FileTypeRegistry.GetInfo(ext);
        var caption = string.Format("{0} {1}\n💾 {2}", typeInfo.Icon, fileInfo.Name, FilesCommand.FormatSize(fileInfo.Length));

        var buttons = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📥 Скачать", string.Format("f:dl:{0}", fileId)),
                InlineKeyboardButton.WithCallbackData("🗑 Закрыть", "f:close")
            }
        });

        if (typeInfo.IsPreviewable)
        {
            // Notify user if FFmpeg needs to be downloaded for video preview
            int? downloadMsgId = null;
            if (typeInfo.Category == FileCategory.Video && !FfmpegProvider.IsAvailable)
            {
                var msg = await ctx.Bot.SendMessage(
                    ctx.ChatId,
                    "⏳ Загрузка FFmpeg (первый запуск)...\n▱▱▱▱▱▱▱▱▱▱ 0%",
                    cancellationToken: ctx.CancellationToken);
                downloadMsgId = msg.Id;
            }

            // Progress callback for FFmpeg download
            Func<long, long?, Task>? onProgress = null;
            if (downloadMsgId != null)
            {
                var msgId = downloadMsgId.Value;
                var chatId = ctx.ChatId;
                var ct2 = ctx.CancellationToken;
                onProgress = async (downloaded, total) =>
                {
                    try
                    {
                        var downloadedMb = downloaded / (1024.0 * 1024.0);
                        string text;
                        if (total is > 0)
                        {
                            var pct = (int)(downloaded * 100 / total.Value);
                            var totalMb = total.Value / (1024.0 * 1024.0);
                            var filled = pct / 10;
                            var bar = new string('▰', filled) + new string('▱', 10 - filled);
                            text = $"⏳ Загрузка FFmpeg (первый запуск)...\n{bar} {pct}%  ({downloadedMb:F1} / {totalMb:F1} МБ)";
                        }
                        else
                        {
                            text = $"⏳ Загрузка FFmpeg (первый запуск)...\n📦 {downloadedMb:F1} МБ загружено";
                        }
                        await ctx.Bot.EditMessageText(chatId, msgId, text, cancellationToken: ct2);
                    }
                    catch { /* Telegram rate limit or message deleted — ignore */ }
                };
            }

            var thumbPath = Path.Combine(Path.GetTempPath(), string.Format("trc_thumb_{0}.jpg", Guid.NewGuid().ToString("N")));
            try
            {
                // Pre-download FFmpeg with progress if needed
                if (downloadMsgId != null)
                    await FfmpegProvider.GetFfmpegPathAsync(ctx.CancellationToken, onProgress);

                var result = await ThumbnailHelper.TryGenerateThumbnailAsync(filePath, thumbPath, ct: ctx.CancellationToken);

                // Clean up the download notification
                if (downloadMsgId != null)
                {
                    try { await ctx.Bot.DeleteMessage(ctx.ChatId, downloadMsgId.Value, ctx.CancellationToken); }
                    catch { }
                }

                if (result)
                {
                    using var thumbStream = File.OpenRead(thumbPath);
                    await ctx.Bot.SendPhoto(
                        ctx.ChatId,
                        InputFile.FromStream(thumbStream, "preview.jpg"),
                        caption: caption,
                        replyMarkup: buttons,
                        cancellationToken: ctx.CancellationToken);
                    return;
                }
            }
            finally
            {
                if (File.Exists(thumbPath))
                    File.Delete(thumbPath);
            }
        }

        // Текстовый файл — показать содержимое в code block
        if (typeInfo.IsTextReadable)
        {
            try
            {
                var content = await System.IO.File.ReadAllTextAsync(filePath, ctx.CancellationToken);
                var langHint = Path.GetExtension(filePath).TrimStart('.');

                // Telegram message limit: 4096 chars. Reserve space for caption + formatting.
                const int maxChars = 3800;
                var truncated = false;
                if (content.Length > maxChars)
                {
                    content = content[..maxChars];
                    truncated = true;
                }

                var text = string.Format("{0}\n\n```{1}\n{2}\n```", caption, langHint, content);
                if (truncated)
                    text += string.Format("\n\n⚠️ _Показаны первые {0} символов из {1}_",
                        maxChars, fileInfo.Length);

                await ctx.Bot.SendMessage(
                    ctx.ChatId,
                    text,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: buttons,
                    cancellationToken: ctx.CancellationToken);
                return;
            }
            catch { /* encoding issues etc. — fall through to plain caption */ }
        }

        // Не превьюшка, не текст, или чтение не удалось — текстовое сообщение
        await ctx.Bot.SendMessage(
            ctx.ChatId,
            caption,
            replyMarkup: buttons,
            cancellationToken: ctx.CancellationToken);
    }

    private async Task SendFileAsync(CallbackContext ctx, string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        if (fileInfo.Length > 50 * 1024 * 1024)
        {
            await ctx.Bot.SendMessage(ctx.ChatId,
                string.Format("Файл слишком большой: {0}\nМаксимум: 50 MB", FilesCommand.FormatSize(fileInfo.Length)),
                cancellationToken: ctx.CancellationToken);
            return;
        }

        using var stream = File.OpenRead(filePath);
        await ctx.Bot.SendDocument(ctx.ChatId,
            InputFile.FromStream(stream, fileInfo.Name),
            caption: string.Format("📄 {0}\n💾 {1}", fileInfo.Name, FilesCommand.FormatSize(fileInfo.Length)),
            cancellationToken: ctx.CancellationToken);
    }

    private async Task SendZipAsync(CallbackContext ctx, string path)
    {
        var dirInfo = new DirectoryInfo(path);
        var tempZip = Path.Combine(Path.GetTempPath(), string.Format("{0}_{1:yyyyMMdd_HHmmss}.zip", dirInfo.Name, DateTime.Now));

        try
        {
            ZipFile.CreateFromDirectory(path, tempZip, CompressionLevel.Fastest, true);

            var zipInfo = new FileInfo(tempZip);
            if (zipInfo.Length > 50 * 1024 * 1024)
            {
                await ctx.Bot.SendMessage(ctx.ChatId,
                    string.Format("Архив слишком большой: {0}", FilesCommand.FormatSize(zipInfo.Length)),
                    cancellationToken: ctx.CancellationToken);
                return;
            }

            using var stream = File.OpenRead(tempZip);
            await ctx.Bot.SendDocument(ctx.ChatId,
                InputFile.FromStream(stream, zipInfo.Name),
                caption: string.Format("📦 {0}\n💾 {1}", dirInfo.Name, FilesCommand.FormatSize(zipInfo.Length)),
                cancellationToken: ctx.CancellationToken);
        }
        finally
        {
            if (File.Exists(tempZip))
                File.Delete(tempZip);
        }
    }

    private string TruncateName(string name, int maxLen)
    {
        if (name.Length <= maxLen) return name;
        return name.Substring(0, maxLen - 3) + "...";
    }

    private string TruncatePath(string path, int maxLen)
    {
        if (path.Length <= maxLen) return path;
        return "..." + path.Substring(path.Length - maxLen + 3);
    }

    private class FileSession
    {
        public string CurrentPath { get; set; } = "C:\\";
        public int CurrentPage { get; set; } = 0;
        private Dictionary<int, string> _pathCache = new();
        private int _nextId = 0;

        public int CachePath(string path)
        {
            var id = _nextId++;
            _pathCache[id] = path;
            return id;
        }

        public string? GetPath(int id)
        {
            return _pathCache.TryGetValue(id, out var path) ? path : null;
        }

        public void ClearCache()
        {
            _pathCache.Clear();
            _nextId = 0;
        }
    }

    private class FileSystemEntry
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
    }
}
