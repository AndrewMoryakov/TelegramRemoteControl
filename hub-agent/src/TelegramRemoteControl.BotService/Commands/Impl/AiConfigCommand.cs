using System.Text.Json;
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
    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public static (string Text, InlineKeyboardMarkup Keyboard) BuildCard(string? jsonPayload)
    {
        var cfg = string.IsNullOrEmpty(jsonPayload)
            ? new AiConfigDto()
            : JsonSerializer.Deserialize<AiConfigDto>(jsonPayload, _jsonOpts) ?? new AiConfigDto();

        var modelDisplay = string.IsNullOrEmpty(cfg.Model) ? "_авто_" : $"`{cfg.Model}`";
        var text = $"""
            ⚙️ *AI Настройки*

            🤖 Модель: {modelDisplay}
            🔄 MaxTurns: `{cfg.MaxTurns}`
            ⏱ Таймаут: `{cfg.TimeoutSeconds} сек`
            📍 CLI: `{cfg.CliPath}`
            """;

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🤖 Модель",         "aicfg:pick:model"),
                InlineKeyboardButton.WithCallbackData("🔄 MaxTurns",       "aicfg:pick:maxturns"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⏱ Таймаут",         "aicfg:pick:timeout"),
                InlineKeyboardButton.WithCallbackData("🔄 Обновить",        "aicfg:show"),
            },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu") }
        });

        return (text, keyboard);
    }

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
}

internal record AiConfigDto
{
    public string Model          { get; init; } = string.Empty;
    public int    MaxTurns       { get; init; } = 5;
    public int    TimeoutSeconds { get; init; } = 300;
    public string CliPath        { get; init; } = "claude";
}
