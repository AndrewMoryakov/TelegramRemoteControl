using System.Text.Json;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.BotService;
using TelegramRemoteControl.BotService.Models;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class FileCallbackHandler : ICallbackHandler
{
    private readonly BotSettings _settings;

    public FileCallbackHandler(IOptions<BotSettings> settings)
    {
        _settings = settings.Value;
    }

    public string Prefix => "f";

    public async Task HandleAsync(CallbackContext ctx)
    {
        var parts = ctx.Data.Split(':');
        if (parts.Length < 2)
            return;

        var session = FileSessionManager.Get(ctx.UserId);
        var action = parts[1];

        switch (action)
        {
            case "d":
                await HandleDriveAsync(ctx, session, parts);
                break;
            case "n":
                await HandleNavigateAsync(ctx, session, parts);
                break;
            case "v":
                await HandlePreviewAsync(ctx, session, parts);
                break;
            case "dl":
                await HandleDownloadAsync(ctx, session, parts);
                break;
            case "close":
                await HandleCloseAsync(ctx);
                break;
            case "up":
                await HandleUpAsync(ctx, session);
                break;
            case "root":
                await HandleRootAsync(ctx, session);
                break;
            case "pg":
                await HandlePageAsync(ctx, session, parts);
                break;
            case "refresh":
                await HandleRefreshAsync(ctx, session);
                break;
        }
    }

    private async Task HandleDriveAsync(CallbackContext ctx, FileSessionManager.FileSession session, string[] parts)
    {
        if (parts.Length < 3 || !int.TryParse(parts[2], out var id))
            return;

        if (!session.TryGetPath(id, out var path))
        {
            await TryAnswerAsync(ctx, "Список устарел, отправьте /files");
            return;
        }

        await LoadAndRenderAsync(ctx, session, path, edit: true);
    }

    private async Task HandleNavigateAsync(CallbackContext ctx, FileSessionManager.FileSession session, string[] parts)
    {
        if (parts.Length < 3 || !int.TryParse(parts[2], out var id))
            return;

        if (!session.TryGetPath(id, out var path))
        {
            await TryAnswerAsync(ctx, "Список устарел, отправьте /files");
            return;
        }

        await LoadAndRenderAsync(ctx, session, path, edit: true);
    }

    private async Task HandlePreviewAsync(CallbackContext ctx, FileSessionManager.FileSession session, string[] parts)
    {
        if (parts.Length < 3 || !int.TryParse(parts[2], out var id))
            return;

        if (!session.TryGetPath(id, out var path))
        {
            await TryAnswerAsync(ctx, "Список устарел, отправьте /files");
            return;
        }

        var info = session.Items.FirstOrDefault(i => BuildFullPath(session.CurrentPath, i.Name) == path);
        var fileName = info?.Name ?? Path.GetFileName(path);
        var size = info?.Size ?? 0;
        var ext = Path.GetExtension(fileName).TrimStart('.');

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.FilePreview,
            Parameters = new Dictionary<string, string> { ["path"] = path }
        });

        if (!response.Success)
        {
            await TryAnswerAsync(ctx, response.ErrorMessage ?? "Ошибка");
            return;
        }

        if (response.Type == ResponseType.Text)
        {
            var preview = response.Text ?? "";
            var maxChars = _settings.FilesPreviewMaxChars > 0 ? _settings.FilesPreviewMaxChars : 3500;
            if (preview.Length > maxChars)
                preview = preview[..maxChars];

            var text = FileUi.BuildPreviewText(fileName, size, preview, ext);
            var keyboard = FileUi.BuildPreviewKeyboard(id);

            await ctx.Bot.SendMessage(ctx.ChatId, text,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: ctx.CancellationToken);
        }
        else if (response.Type == ResponseType.Document && response.Data != null)
        {
            await using var stream = new MemoryStream(response.Data);
            await ctx.Bot.SendDocument(ctx.ChatId,
                Telegram.Bot.Types.InputFile.FromStream(stream, response.FileName ?? fileName),
                cancellationToken: ctx.CancellationToken);
        }
        else
        {
            await ctx.Bot.SendMessage(ctx.ChatId, response.Text ?? "✅", cancellationToken: ctx.CancellationToken);
        }

        await TryAnswerAsync(ctx, null);
    }

    private async Task HandleDownloadAsync(CallbackContext ctx, FileSessionManager.FileSession session, string[] parts)
    {
        if (parts.Length < 3 || !int.TryParse(parts[2], out var id))
            return;

        if (!session.TryGetPath(id, out var path))
        {
            await TryAnswerAsync(ctx, "Список устарел, отправьте /files");
            return;
        }

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.FileDownload,
            Parameters = new Dictionary<string, string> { ["path"] = path }
        });

        if (!response.Success || response.Data == null)
        {
            await TryAnswerAsync(ctx, response.ErrorMessage ?? "Ошибка");
            return;
        }

        await using var stream = new MemoryStream(response.Data);
        await ctx.Bot.SendDocument(ctx.ChatId,
            Telegram.Bot.Types.InputFile.FromStream(stream, response.FileName ?? Path.GetFileName(path)),
            cancellationToken: ctx.CancellationToken);

        await TryAnswerAsync(ctx, null);
    }

    private async Task HandleCloseAsync(CallbackContext ctx)
    {
        try
        {
            if (ctx.MessageId.HasValue)
                await ctx.Bot.DeleteMessage(ctx.ChatId, ctx.MessageId.Value, ctx.CancellationToken);
        }
        catch
        {
            // ignore
        }
    }

    private async Task HandleUpAsync(CallbackContext ctx, FileSessionManager.FileSession session)
    {
        if (string.IsNullOrWhiteSpace(session.CurrentPath))
        {
            await HandleRootAsync(ctx, session);
            return;
        }

        var parent = Directory.GetParent(session.CurrentPath);
        if (parent == null)
        {
            await HandleRootAsync(ctx, session);
            return;
        }

        await LoadAndRenderAsync(ctx, session, parent.FullName, edit: true);
    }

    private async Task HandleRootAsync(CallbackContext ctx, FileSessionManager.FileSession session)
    {
        await LoadAndRenderAsync(ctx, session, null, edit: true);
    }

    private async Task HandlePageAsync(CallbackContext ctx, FileSessionManager.FileSession session, string[] parts)
    {
        if (parts.Length < 3 || !int.TryParse(parts[2], out var page))
            return;

        if (session.IsStale(GetSessionTtl()) || session.Items.Count == 0)
        {
            await LoadAndRenderAsync(ctx, session, session.CurrentPath, edit: true);
            return;
        }

        session.CurrentPage = page;
        var (text, keyboard) = FileUi.BuildDirectory(session, GetPageSize());
        await EditOrSendAsync(ctx, text, keyboard, ParseMode.Markdown);
        await TryAnswerAsync(ctx, null);
    }

    private async Task HandleRefreshAsync(CallbackContext ctx, FileSessionManager.FileSession session)
    {
        await LoadAndRenderAsync(ctx, session, session.CurrentPath, edit: true);
    }

    private async Task LoadAndRenderAsync(CallbackContext ctx, FileSessionManager.FileSession session, string? path, bool edit)
    {
        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.FileList,
            Parameters = path == null ? null : new Dictionary<string, string> { ["path"] = path }
        });

        if (!response.Success)
        {
            await TryAnswerAsync(ctx, response.ErrorMessage ?? "Ошибка");
            return;
        }

        if (string.IsNullOrWhiteSpace(response.JsonPayload))
        {
            await EditOrSendAsync(ctx, "Нет данных", null);
            await TryAnswerAsync(ctx, null);
            return;
        }

        var payload = JsonSerializer.Deserialize<FileListPayload>(
            response.JsonPayload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new FileListPayload();

        session.Set(payload.Path, payload.Items);

        if (string.IsNullOrWhiteSpace(payload.Path))
        {
            var (text, keyboard) = FileUi.BuildDrives(session, payload.Items);
            await EditOrSendAsync(ctx, text, keyboard, ParseMode.Markdown);
        }
        else
        {
            var (text, keyboard) = FileUi.BuildDirectory(session, GetPageSize());
            await EditOrSendAsync(ctx, text, keyboard, ParseMode.Markdown);
        }

        await TryAnswerAsync(ctx, null);
    }

    private async Task EditOrSendAsync(
        CallbackContext ctx,
        string text,
        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup? keyboard,
        ParseMode? parseMode = null)
    {
        if (ctx.MessageId.HasValue)
        {
            try
            {
                if (parseMode.HasValue)
                {
                    await ctx.Bot.EditMessageText(ctx.ChatId, ctx.MessageId.Value, text,
                        parseMode: parseMode.Value,
                        replyMarkup: keyboard,
                        cancellationToken: ctx.CancellationToken);
                }
                else
                {
                    await ctx.Bot.EditMessageText(ctx.ChatId, ctx.MessageId.Value, text,
                        replyMarkup: keyboard,
                        cancellationToken: ctx.CancellationToken);
                }
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex)
                when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
            {
                // ignore no-op edits
            }
        }
        else
        {
            if (parseMode.HasValue)
            {
                await ctx.Bot.SendMessage(ctx.ChatId, text,
                    parseMode: parseMode.Value,
                    replyMarkup: keyboard,
                    cancellationToken: ctx.CancellationToken);
            }
            else
            {
                await ctx.Bot.SendMessage(ctx.ChatId, text,
                    replyMarkup: keyboard,
                    cancellationToken: ctx.CancellationToken);
            }
        }
    }

    private static async Task TryAnswerAsync(CallbackContext ctx, string? text)
    {
        try
        {
            await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, text, cancellationToken: ctx.CancellationToken);
        }
        catch
        {
            // ignore expired callback
        }
    }

    private static string BuildFullPath(string? basePath, string name)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            return name;

        return Path.Combine(basePath, name);
    }

    private int GetPageSize() => _settings.FilesPageSize > 0 ? _settings.FilesPageSize : 8;

    private TimeSpan GetSessionTtl()
    {
        var minutes = _settings.FilesSessionTtlMinutes;
        if (minutes <= 0) minutes = 2;
        return TimeSpan.FromMinutes(minutes);
    }
}
