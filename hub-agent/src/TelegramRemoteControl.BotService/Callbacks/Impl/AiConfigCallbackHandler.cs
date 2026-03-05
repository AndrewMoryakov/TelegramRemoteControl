using System.Collections.Concurrent;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Commands.Impl;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class AiConfigCallbackHandler : ICallbackHandler
{
    // userId → list of (id, name) fetched from OpenRouter
    private static readonly ConcurrentDictionary<long, List<(string Id, string Name)>> _modelCache = new();

    public string Prefix => "aicfg";

    public async Task HandleAsync(CallbackContext ctx)
    {
        // Formats: "aicfg:show" | "aicfg:pick:X" | "aicfg:set:key:value"
        //          "aicfg:apikey" | "aicfg:models:N" | "aicfg:selmodel:N" | "aicfg:refreshmodels"
        var parts  = ctx.Data.Split(':', 4);
        var action = parts.Length > 1 ? parts[1] : string.Empty;

        switch (action)
        {
            case "show":
                await HandleShowAsync(ctx);
                break;

            case "pick" when parts.Length > 2:
                await HandlePickAsync(ctx, parts[2]);
                break;

            case "set" when parts.Length > 3:
                await HandleSetAsync(ctx, parts[2], parts[3]);
                break;

            case "apikey":
                await HandleApiKeyPromptAsync(ctx);
                break;

            case "models" when parts.Length > 2 && int.TryParse(parts[2], out var page):
                await HandleModelsPageAsync(ctx, page, forceRefresh: false);
                break;

            case "refreshmodels":
                _modelCache.TryRemove(ctx.UserId, out _);
                await HandleModelsPageAsync(ctx, 0, forceRefresh: true);
                break;

            case "selmodel" when parts.Length > 2 && int.TryParse(parts[2], out var idx):
                await HandleSelectModelAsync(ctx, idx);
                break;

            default:
                await TryAnswerAsync(ctx, null);
                break;
        }
    }

    // ── Show ──────────────────────────────────────────────────────────────────

    private async Task HandleShowAsync(CallbackContext ctx)
    {
        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId      = ctx.UserId,
            CommandType = CommandType.AgentConfig,
            Arguments   = "get"
        });

        if (!response.Success)
        {
            await TryAnswerAsync(ctx, $"❌ {response.ErrorMessage}");
            return;
        }

        var (text, keyboard) = AiConfigUi.BuildCard(response.JsonPayload);
        await EditOrSendAsync(ctx, text, keyboard, ParseMode.Markdown);
        await TryAnswerAsync(ctx, null);
    }

    // ── Pick (show picker keyboard) ────────────────────────────────────────────

    private static async Task HandlePickAsync(CallbackContext ctx, string param)
    {
        InlineKeyboardMarkup keyboard;
        string text;

        switch (param)
        {
            case "provider":
                text     = "🔌 Выберите провайдер:";
                keyboard = AiConfigUi.BuildProviderKeyboard();
                break;
            case "model":
                text     = "🤖 Выберите модель Claude:";
                keyboard = AiConfigUi.BuildModelKeyboard();
                break;
            case "maxturns":
                text     = "🔄 Выберите количество ходов:";
                keyboard = AiConfigUi.BuildMaxTurnsKeyboard();
                break;
            case "timeout":
                text     = "⏱ Выберите таймаут:";
                keyboard = AiConfigUi.BuildTimeoutKeyboard();
                break;
            default:
                await TryAnswerAsync(ctx, null);
                return;
        }

        await EditOrSendAsync(ctx, text, keyboard);
        await TryAnswerAsync(ctx, null);
    }

    // ── Set ───────────────────────────────────────────────────────────────────

    private async Task HandleSetAsync(CallbackContext ctx, string key, string value)
    {
        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId      = ctx.UserId,
            CommandType = CommandType.AgentConfig,
            Arguments   = $"set:{key}:{value}"
        });

        if (!response.Success)
        {
            await TryAnswerAsync(ctx, $"❌ {response.ErrorMessage}");
            return;
        }

        await TryAnswerAsync(ctx, "✅ Сохранено");
        await HandleShowAsync(ctx);
    }

    // ── API Key prompt ─────────────────────────────────────────────────────────

    private static async Task HandleApiKeyPromptAsync(CallbackContext ctx)
    {
        AiKeyInputSession.Start(ctx.UserId, ctx.ChatId);
        await TryAnswerAsync(ctx, null);
        await ctx.Bot.SendMessage(ctx.ChatId,
            "🔑 Введите OpenRouter API ключ:\n\n" +
            "_Получить ключ можно на_ openrouter.ai/keys\n\n" +
            "Отправьте ключ сообщением или /cancel для отмены",
            parseMode: ParseMode.Markdown,
            cancellationToken: ctx.CancellationToken);
    }

    // ── Models list ────────────────────────────────────────────────────────────

    private async Task HandleModelsPageAsync(CallbackContext ctx, int page, bool forceRefresh)
    {
        List<(string Id, string Name)>? models;

        if (!forceRefresh && _modelCache.TryGetValue(ctx.UserId, out var cached))
        {
            models = cached;
        }
        else
        {
            await TryAnswerAsync(ctx, "⏳ Загружаю список моделей...");

            var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
            {
                UserId      = ctx.UserId,
                CommandType = CommandType.AgentConfig,
                Arguments   = "get-models"
            });

            if (!response.Success)
            {
                await TryAnswerAsync(ctx, $"❌ {response.ErrorMessage}");
                await ctx.Bot.SendMessage(ctx.ChatId, $"❌ {response.ErrorMessage}",
                    cancellationToken: ctx.CancellationToken);
                return;
            }

            models = ParseModels(response.JsonPayload);
            _modelCache[ctx.UserId] = models;
        }

        if (models.Count == 0)
        {
            await TryAnswerAsync(ctx, "Список моделей пуст");
            return;
        }

        var (text, keyboard) = AiConfigUi.BuildModelsPage(models, page);
        await EditOrSendAsync(ctx, text, keyboard, ParseMode.Markdown);
        await TryAnswerAsync(ctx, null);
    }

    private async Task HandleSelectModelAsync(CallbackContext ctx, int idx)
    {
        if (!_modelCache.TryGetValue(ctx.UserId, out var models) || idx >= models.Count)
        {
            await TryAnswerAsync(ctx, "Обновите список моделей");
            return;
        }

        var (modelId, modelName) = models[idx];

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId      = ctx.UserId,
            CommandType = CommandType.AgentConfig,
            Arguments   = $"set:model:{modelId}"
        });

        if (!response.Success)
        {
            await TryAnswerAsync(ctx, $"❌ {response.ErrorMessage}");
            return;
        }

        await TryAnswerAsync(ctx, $"✅ {modelName}");
        await HandleShowAsync(ctx);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static List<(string Id, string Name)> ParseModels(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new();

        try
        {
            using var doc  = JsonDocument.Parse(json);
            var data       = doc.RootElement.GetProperty("data");
            var list       = new List<(string Id, string Name)>();

            foreach (var item in data.EnumerateArray())
            {
                var id   = item.TryGetProperty("id",   out var idProp)   ? idProp.GetString()   ?? "" : "";
                var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : id;
                if (!string.IsNullOrEmpty(id))
                    list.Add((id, name));
            }

            return list;
        }
        catch
        {
            return new();
        }
    }

    private static async Task EditOrSendAsync(
        CallbackContext ctx,
        string text,
        InlineKeyboardMarkup keyboard,
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
                // ignore
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
        try { await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, text, cancellationToken: ctx.CancellationToken); }
        catch { }
    }
}
