using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Commands.Impl;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class AiConfigCallbackHandler : ICallbackHandler
{
    public string Prefix => "aicfg";

    public async Task HandleAsync(CallbackContext ctx)
    {
        // Formats: "aicfg:show" | "aicfg:pick:model" | "aicfg:set:model:VALUE"
        var parts = ctx.Data.Split(':', 4);
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

            default:
                await TryAnswerAsync(ctx, null);
                break;
        }
    }

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

    private static async Task HandlePickAsync(CallbackContext ctx, string param)
    {
        InlineKeyboardMarkup keyboard;
        string text;

        switch (param)
        {
            case "model":
                text     = "🤖 Выберите модель:";
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

        // Refresh the settings card
        await HandleShowAsync(ctx);
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
            // ignore
        }
    }
}
