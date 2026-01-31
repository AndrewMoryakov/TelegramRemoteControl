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
            case "min":
            case "max":
            case "restore":
            case "close":
                await HandleActionAsync(ctx, action, parts);
                break;
            case "minall":
                await HandleMinAllAsync(ctx);
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

        WindowCache.Set(ctx.UserId, windows);

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

        // After close -> refresh list, otherwise refresh info
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

    private async Task<WindowInfo?> FindWindowAsync(CallbackContext ctx, long hwnd)
    {
        if (WindowCache.TryGet(ctx.UserId, out var cached))
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

        WindowCache.Set(ctx.UserId, windows);
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
            if (parseMode.HasValue)
            {
                try
                {
                    await ctx.Bot.EditMessageText(ctx.ChatId, ctx.MessageId.Value, text,
                        parseMode: parseMode.Value,
                        replyMarkup: keyboard,
                        cancellationToken: ctx.CancellationToken);
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
                {
                    // ignore no-op edits
                }
            }
            else
            {
                try
                {
                    await ctx.Bot.EditMessageText(ctx.ChatId, ctx.MessageId.Value, text,
                        replyMarkup: keyboard,
                        cancellationToken: ctx.CancellationToken);
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
                {
                    // ignore no-op edits
                }
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
}
