using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.BotService.Models;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class SvcCallbackHandler : ICallbackHandler
{
    public string Prefix => "svc";

    public async Task HandleAsync(CallbackContext ctx)
    {
        var parts = ctx.Data.Split(':');
        if (parts.Length < 3)
            return;

        var action = parts[1];
        var name = parts[2];

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.ServiceAction,
            Parameters = new Dictionary<string, string>
            {
                ["action"] = action,
                ["name"] = name
            }
        });

        if (!response.Success)
        {
            await TryAnswerAsync(ctx, response.ErrorMessage ?? "Ошибка");
            return;
        }

        // Refresh list for updated status
        var listResponse = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.Services
        });

        if (!listResponse.Success || string.IsNullOrWhiteSpace(listResponse.JsonPayload))
        {
            await TryAnswerAsync(ctx, response.Text ?? "✅ Готово");
            return;
        }

        var services = JsonSerializer.Deserialize<List<ServiceInfo>>(
            listResponse.JsonPayload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ServiceInfo>();

        ServiceCache.Set(ctx.UserId, services);

        var svc = services.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (svc == null)
        {
            await TryAnswerAsync(ctx, "Служба не найдена");
            return;
        }

        var detail = ServiceUi.BuildInfo(svc);
        if (ctx.MessageId.HasValue)
        {
            await ctx.Bot.EditMessageText(ctx.ChatId, ctx.MessageId.Value,
                detail.Text,
                parseMode: ParseMode.Markdown,
                replyMarkup: detail.Keyboard,
                cancellationToken: ctx.CancellationToken);
        }
        else
        {
            await ctx.Bot.SendMessage(ctx.ChatId,
                detail.Text,
                parseMode: ParseMode.Markdown,
                replyMarkup: detail.Keyboard,
                cancellationToken: ctx.CancellationToken);
        }

        await TryAnswerAsync(ctx, response.Text ?? "✅ Готово");
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
