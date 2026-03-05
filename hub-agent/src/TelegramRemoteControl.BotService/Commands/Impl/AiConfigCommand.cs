using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class AiConfigCommand : ICommand
{
    public string Id => "ai_config";
    public string[] Aliases => new[] { "/aiconfig" };
    public string Title => "AI Настройки";
    public string? Icon => "⚙️";
    public string? Description => "Параметры AI агента";
    public string Category => Categories.Ai;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId      = ctx.UserId,
            CommandType = CommandType.AgentConfig,
            Arguments   = "get"
        });

        if (!response.Success)
        {
            await ctx.ReplyWithBack($"❌ {response.ErrorMessage}");
            return;
        }

        var (text, keyboard) = AiConfigUi.BuildCard(response.JsonPayload);
        await ctx.Bot.SendMessage(ctx.ChatId, text,
            parseMode: ParseMode.Markdown,
            replyMarkup: keyboard,
            cancellationToken: ctx.CancellationToken);
    }
}

internal static class AiConfigUi
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static (string Text, InlineKeyboardMarkup Keyboard) BuildCard(string? jsonPayload)
    {
        var cfg = string.IsNullOrEmpty(jsonPayload)
            ? new AiConfigDto()
            : JsonSerializer.Deserialize<AiConfigDto>(jsonPayload, JsonOpts) ?? new AiConfigDto();

        bool isOpenRouter = cfg.Provider == "openrouter";

        var providerLine = isOpenRouter
            ? "🔌 Провайдер: `🌐 OpenRouter`"
            : "🔌 Провайдер: `🤖 Claude (Anthropic)`";

        var modelDisplay = string.IsNullOrEmpty(cfg.Model)
            ? (isOpenRouter ? "_не выбрана_" : "_авто_")
            : $"`{cfg.Model}`";

        var apiKeyLine = isOpenRouter
            ? (cfg.HasOpenRouterKey ? "\n🔑 API Key: ✅ задан" : "\n🔑 API Key: ❌ не задан")
            : string.Empty;

        var cliLine = isOpenRouter ? string.Empty : $"\n📍 CLI: `{cfg.CliPath}`";

        var text = $"""
            ⚙️ *AI Настройки*

            {providerLine}{apiKeyLine}
            🤖 Модель: {modelDisplay}
            🔄 MaxTurns: `{cfg.MaxTurns}`
            ⏱ Таймаут: `{cfg.TimeoutSeconds} сек`{cliLine}
            """;

        var rows = new List<InlineKeyboardButton[]>
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔌 Провайдер", "aicfg:pick:provider"),
                InlineKeyboardButton.WithCallbackData("🔄 Обновить",  "aicfg:show"),
            }
        };

        if (isOpenRouter)
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("🔑 API Key",          "aicfg:apikey"),
                InlineKeyboardButton.WithCallbackData("📋 Выбрать модель",   "aicfg:models:0"),
            });
        }
        else
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("🤖 Модель", "aicfg:pick:model"),
            });
        }

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔄 MaxTurns", "aicfg:pick:maxturns"),
            InlineKeyboardButton.WithCallbackData("⏱ Таймаут",  "aicfg:pick:timeout"),
        });

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu") });

        return (text, new InlineKeyboardMarkup(rows));
    }

    public static InlineKeyboardMarkup BuildProviderKeyboard() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🤖 Claude (Anthropic)", "aicfg:set:provider:claude") },
            new[] { InlineKeyboardButton.WithCallbackData("🌐 OpenRouter",         "aicfg:set:provider:openrouter") },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад",              "aicfg:show") }
        });

    public static InlineKeyboardMarkup BuildModelKeyboard() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✨ Default (авто)",       "aicfg:set:model:") },
            new[] { InlineKeyboardButton.WithCallbackData("claude-opus-4-6",         "aicfg:set:model:claude-opus-4-6") },
            new[] { InlineKeyboardButton.WithCallbackData("claude-sonnet-4-6",       "aicfg:set:model:claude-sonnet-4-6") },
            new[] { InlineKeyboardButton.WithCallbackData("claude-haiku-4-5",        "aicfg:set:model:claude-haiku-4-5-20251001") },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад",               "aicfg:show") }
        });

    public static InlineKeyboardMarkup BuildMaxTurnsKeyboard() =>
        new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("1",  "aicfg:set:maxturns:1"),
                InlineKeyboardButton.WithCallbackData("3",  "aicfg:set:maxturns:3"),
                InlineKeyboardButton.WithCallbackData("5",  "aicfg:set:maxturns:5"),
                InlineKeyboardButton.WithCallbackData("10", "aicfg:set:maxturns:10"),
                InlineKeyboardButton.WithCallbackData("20", "aicfg:set:maxturns:20"),
            },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "aicfg:show") }
        });

    public static InlineKeyboardMarkup BuildTimeoutKeyboard() =>
        new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("60 сек",  "aicfg:set:timeout:60"),
                InlineKeyboardButton.WithCallbackData("120 сек", "aicfg:set:timeout:120"),
                InlineKeyboardButton.WithCallbackData("300 сек", "aicfg:set:timeout:300"),
                InlineKeyboardButton.WithCallbackData("600 сек", "aicfg:set:timeout:600"),
            },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "aicfg:show") }
        });

    public static (string Text, InlineKeyboardMarkup Keyboard) BuildModelsPage(
        List<(string Id, string Name)> models, int page)
    {
        const int pageSize = 8;
        int totalPages = Math.Max(1, (models.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 0, totalPages - 1);

        var pageModels = models.Skip(page * pageSize).Take(pageSize).ToList();
        var rows = new List<InlineKeyboardButton[]>();

        for (int i = 0; i < pageModels.Count; i++)
        {
            var (id, name) = pageModels[i];
            var label = name.Length > 38 ? name[..35] + "..." : name;
            var globalIdx = page * pageSize + i;
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(label, $"aicfg:selmodel:{globalIdx}") });
        }

        // Navigation
        var navRow = new List<InlineKeyboardButton>();
        if (page > 0)
            navRow.Add(InlineKeyboardButton.WithCallbackData("◀️", $"aicfg:models:{page - 1}"));
        navRow.Add(InlineKeyboardButton.WithCallbackData($"{page + 1}/{totalPages}", "aicfg:show"));
        if (page < totalPages - 1)
            navRow.Add(InlineKeyboardButton.WithCallbackData("▶️", $"aicfg:models:{page + 1}"));
        rows.Add(navRow.ToArray());

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔄 Обновить список", "aicfg:refreshmodels"),
            InlineKeyboardButton.WithCallbackData("◀️ Назад",           "aicfg:show")
        });

        var text = $"📋 *Модели OpenRouter* (стр. {page + 1}/{totalPages})\n\nВыберите модель:";
        return (text, new InlineKeyboardMarkup(rows));
    }
}

internal record AiConfigDto
{
    public string Provider        { get; init; } = "claude";
    public string Model           { get; init; } = string.Empty;
    public int    MaxTurns        { get; init; } = 5;
    public int    TimeoutSeconds  { get; init; } = 300;
    public string CliPath         { get; init; } = "claude";
    public bool   HasOpenRouterKey{ get; init; }
}
