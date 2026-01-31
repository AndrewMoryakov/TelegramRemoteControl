using Telegram.Bot;
using TelegramRemoteControl.Shared.Contracts.HubApi;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class NotifyCallbackHandler : ICallbackHandler
{
    public string Prefix => "notify";

    public async Task HandleAsync(CallbackContext ctx)
    {
        var action = ctx.Data.Length > "notify:".Length ? ctx.Data["notify:".Length..] : string.Empty;
        action = action.Trim().ToLowerInvariant();

        if (action is not ("on" or "off"))
            return;

        var enabled = action == "on";
        await ctx.Hub.SetUserNotify(new UserNotifyRequest
        {
            UserId = ctx.UserId,
            Enabled = enabled
        });

        var text = enabled ? "✅ Уведомления включены" : "✅ Уведомления выключены";
        await ctx.Bot.SendMessage(ctx.ChatId, text, cancellationToken: ctx.CancellationToken);

        try
        {
            await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, cancellationToken: ctx.CancellationToken);
        }
        catch
        {
            // ignore expired callback
        }
    }
}
