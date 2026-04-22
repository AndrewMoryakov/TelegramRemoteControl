using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.BotService.Models;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class WinCallbackHandler : ICallbackHandler
{
    public string Prefix => "win";

    public async Task HandleAsync(CallbackContext ctx)
    {
        var parts = ctx.Data.Split(':');
        if (parts.Length < 2)
            return;

        var action = parts[1];
        switch (action)
        {
            case "list":
                await HandleListAsync(ctx);
                break;
            case "info":
                await HandleInfoAsync(ctx, parts);
                break;
            case "ss":
                await HandleScreenshotAsync(ctx, parts);
                break;
            case "focus":
                await HandleActionAsync(ctx, "focus", parts);
                break;
            case "min":
            case "max":
            case "restore":
            case "close":
                await HandleActionAsync(ctx, action, parts);
                break;
            case "minall":
                await HandleMinAllAsync(ctx);
                break;
            case "type":
                await HandleTypePromptAsync(ctx, parts);
                break;
            case "keys":
                await HandleKeysMenuAsync(ctx, parts);
                break;
            case "key":
                await HandleKeyAsync(ctx, parts);
                break;
        }
    }

    private async Task HandleListAsync(CallbackContext ctx)
    {
        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.WindowsList
        });

        if (!response.Success)
        {
            await TryAnswerAsync(ctx, $"❌ {response.ErrorMessage}");
            return;
        }

        if (string.IsNullOrWhiteSpace(response.JsonPayload))
        {
            await EditOrSendAsync(ctx, response.Text ?? "Нет данных", null);
            await TryAnswerAsync(ctx, null);
            return;
        }

        var windows = JsonSerializer.Deserialize<List<WindowInfo>>(
            response.JsonPayload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<WindowInfo>();

        if (windows.Count == 0)
        {
            await EditOrSendAsync(ctx, "🪟 Нет открытых окон", null);
            await TryAnswerAsync(ctx, null);
            return;
        }

        var listAgentId = (await ctx.Hub.GetSelectedDevice(ctx.UserId))?.AgentId;
        WindowCache.Set(ctx.UserId, listAgentId, windows);

        var (text, keyboard) = WindowListUi.BuildList(windows);
        await EditOrSendAsync(ctx, text, keyboard, ParseMode.Markdown);
        await TryAnswerAsync(ctx, null);
    }

    private async Task HandleInfoAsync(CallbackContext ctx, string[] parts)
    {
        if (parts.Length < 3 || !long.TryParse(parts[2], out var hwnd))
            return;

        var win = await FindWindowAsync(ctx, hwnd);
        if (win == null)
        {
            await TryAnswerAsync(ctx, "Окно не найдено, обновите список");
            return;
        }

        var result = WindowListUi.BuildInfo(win);
        if (result == null)
            return;

        await EditOrSendAsync(ctx, result.Value.Text, result.Value.Keyboard, ParseMode.Markdown);
        await TryAnswerAsync(ctx, null);
    }

    private async Task HandleScreenshotAsync(CallbackContext ctx, string[] parts)
    {
        if (parts.Length < 3 || !long.TryParse(parts[2], out var hwnd))
            return;

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.WindowScreenshot,
            Arguments = hwnd.ToString()
        });

        if (!response.Success || response.Data == null || response.Data.Length == 0)
        {
            await TryAnswerAsync(ctx, response.ErrorMessage ?? "Ошибка скриншота");
            return;
        }

        await using var stream = new MemoryStream(response.Data);
        await ctx.Bot.SendPhoto(ctx.ChatId,
            Telegram.Bot.Types.InputFile.FromStream(stream, response.FileName ?? "window.png"),
            cancellationToken: ctx.CancellationToken);

        await TryAnswerAsync(ctx, null);
    }

    private async Task HandleActionAsync(CallbackContext ctx, string action, string[] parts)
    {
        if (parts.Length < 3 || !long.TryParse(parts[2], out var hwnd))
            return;

        // BL-12: block stale buttons from close/min/max/focus/restore landing on
        // a different PC. FindWindowAsync keys the cache by (userId, selectedAgentId)
        // and will only find hwnd if it exists on the currently selected device.
        var win = await FindWindowAsync(ctx, hwnd);
        if (win == null)
        {
            await TryAnswerAsync(ctx, "Список устарел, повторите /win");
            return;
        }

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.WindowAction,
            Arguments = $"{action}:{hwnd}"
        });

        if (!response.Success)
        {
            await TryAnswerAsync(ctx, response.ErrorMessage ?? "Ошибка");
            return;
        }

        if (action == "close")
            await HandleListAsync(ctx);
        else
            await HandleInfoAsync(ctx, new[] { "win", "info", hwnd.ToString() });
    }

    private async Task HandleMinAllAsync(CallbackContext ctx)
    {
        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.WindowAction,
            Arguments = "minall"
        });

        await TryAnswerAsync(ctx, response.Success ? "✅ Все окна свёрнуты" : response.ErrorMessage);
    }

    private async Task HandleTypePromptAsync(CallbackContext ctx, string[] parts)
    {
        if (parts.Length < 3 || !long.TryParse(parts[2], out var hwnd))
            return;

        WindowTypeSession.Start(ctx.UserId, hwnd, ctx.ChatId);

        await TryAnswerAsync(ctx, null);
        await ctx.Bot.SendMessage(ctx.ChatId,
            "✏️ Введите текст для отправки в окно:\n\n_Отправьте сообщение или_ /cancel _для отмены_",
            parseMode: ParseMode.Markdown,
            cancellationToken: ctx.CancellationToken);
    }

    private async Task HandleKeysMenuAsync(CallbackContext ctx, string[] parts)
    {
        if (parts.Length < 3 || !long.TryParse(parts[2], out var hwnd))
            return;

        var win = await FindWindowAsync(ctx, hwnd);
        var title = win != null
            ? (win.Title.Length > 25 ? win.Title[..22] + "..." : win.Title)
            : hwnd.ToString();

        var keyboard = WindowListUi.BuildKeys(hwnd);
        await EditOrSendAsync(ctx, $"🎹 Клавиши → *{EscapeMarkdown(title)}*", keyboard, ParseMode.Markdown);
        await TryAnswerAsync(ctx, null);
    }

    private async Task HandleKeyAsync(CallbackContext ctx, string[] parts)
    {
        // parts: ["win", "key", "hwnd", "KeyName"]
        if (parts.Length < 4 || !long.TryParse(parts[2], out var hwnd))
            return;

        var keyName = parts[3];

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.WindowAction,
            Arguments = $"key:{hwnd}:{keyName}"
        });

        await TryAnswerAsync(ctx, response.Success ? $"✅ {keyName}" : $"❌ {response.ErrorMessage}");
        // Stay on the keys keyboard — no page change needed
    }

    private async Task<WindowInfo?> FindWindowAsync(CallbackContext ctx, long hwnd)
    {
        var agentId = (await ctx.Hub.GetSelectedDevice(ctx.UserId))?.AgentId;
        if (WindowCache.TryGet(ctx.UserId, agentId, out var cached))
            return cached.FirstOrDefault(w => w.Hwnd == hwnd);

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.WindowsList
        });

        if (!response.Success || string.IsNullOrWhiteSpace(response.JsonPayload))
            return null;

        var windows = JsonSerializer.Deserialize<List<WindowInfo>>(
            response.JsonPayload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<WindowInfo>();

        WindowCache.Set(ctx.UserId, agentId, windows);
        return windows.FirstOrDefault(w => w.Hwnd == hwnd);
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
                    await ctx.Bot.EditMessageText(ctx.ChatId, ctx.MessageId.Value, text,
                        parseMode: parseMode.Value, replyMarkup: keyboard,
                        cancellationToken: ctx.CancellationToken);
                else
                    await ctx.Bot.EditMessageText(ctx.ChatId, ctx.MessageId.Value, text,
                        replyMarkup: keyboard, cancellationToken: ctx.CancellationToken);
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
                await ctx.Bot.SendMessage(ctx.ChatId, text,
                    parseMode: parseMode.Value, replyMarkup: keyboard,
                    cancellationToken: ctx.CancellationToken);
            else
                await ctx.Bot.SendMessage(ctx.ChatId, text,
                    replyMarkup: keyboard, cancellationToken: ctx.CancellationToken);
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

    private static string EscapeMarkdown(string text) =>
        text.Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`").Replace("[", "\\[");
}
