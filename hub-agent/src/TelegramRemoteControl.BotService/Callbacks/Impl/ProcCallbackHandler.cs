using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.BotService.Models;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class ProcCallbackHandler : ICallbackHandler
{
    public string Prefix => "proc";

    public async Task HandleAsync(CallbackContext ctx)
    {
        var parts = ctx.Data.Split(':');
        if (parts.Length < 2)
            return;

        var action = parts[1];
        switch (action)
        {
            case "info":
                await HandleInfoAsync(ctx, parts);
                break;
            case "kill":
                await HandleKillAsync(ctx, parts);
                break;
        }
    }

    private async Task HandleInfoAsync(CallbackContext ctx, string[] parts)
    {
        if (!ProcessCache.TryGet(ctx.UserId, out var items))
        {
            await TryAnswerAsync(ctx, "Список устарел, отправьте /processes");
            return;
        }

        if (parts.Length < 3 || !int.TryParse(parts[2], out var pid))
            return;

        var result = ProcessListUi.BuildInfoByPid(items, pid);
        if (result == null)
        {
            await TryAnswerAsync(ctx, "Процесс не найден, обновите список");
            return;
        }

        if (ctx.MessageId.HasValue)
        {
            await ctx.Bot.EditMessageText(ctx.ChatId, ctx.MessageId.Value,
                result.Value.Text,
                parseMode: ParseMode.Markdown,
                replyMarkup: result.Value.Keyboard,
                cancellationToken: ctx.CancellationToken);
        }
        else
        {
            await ctx.Bot.SendMessage(ctx.ChatId,
                result.Value.Text,
                parseMode: ParseMode.Markdown,
                replyMarkup: result.Value.Keyboard,
                cancellationToken: ctx.CancellationToken);
        }

        await TryAnswerAsync(ctx, null);
    }

    private async Task HandleKillAsync(CallbackContext ctx, string[] parts)
    {
        if (parts.Length < 3 || !int.TryParse(parts[2], out var pid))
            return;

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.Kill,
            Parameters = new Dictionary<string, string> { ["pid"] = pid.ToString() }
        });

        var text = response.Success
            ? $"✅ Процесс {pid} завершён"
            : $"❌ {response.ErrorMessage}";

        if (ctx.MessageId.HasValue)
        {
            await ctx.Bot.EditMessageText(ctx.ChatId, ctx.MessageId.Value,
                text,
                cancellationToken: ctx.CancellationToken);
        }
        else
        {
            await ctx.Bot.SendMessage(ctx.ChatId, text, cancellationToken: ctx.CancellationToken);
        }

        await TryAnswerAsync(ctx, null);
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
