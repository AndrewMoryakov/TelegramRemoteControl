using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.Shared.Contracts.HubApi;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

/// <summary>
/// Обрабатывает admin:approve:userId и admin:deny:userId —
/// кнопки из уведомления о новом пользователе.
/// </summary>
public class AdminUserCallbackHandler : ICallbackHandler
{
    public string Prefix => "admin";

    public async Task HandleAsync(CallbackContext ctx)
    {
        // admin:approve:123 or admin:deny:123
        var parts = ctx.Data.Split(':');
        if (parts.Length < 3 || !long.TryParse(parts[2], out var targetUserId))
        {
            await AnswerAsync(ctx, "❌ Неверный формат");
            return;
        }

        var action = parts[1];
        bool approve = string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase);

        await ctx.Hub.SetUserAuthorized(new UserAuthorizeRequest
        {
            UserId = targetUserId,
            Authorized = approve
        });

        var resultText = approve
            ? $"✅ Пользователь `{targetUserId}` одобрен"
            : $"⛔ Пользователь `{targetUserId}` отклонён";

        // Edit the notification message to remove buttons
        if (ctx.Query.Message != null)
        {
            try
            {
                await ctx.Bot.EditMessageText(
                    ctx.Query.Message.Chat.Id,
                    ctx.Query.Message.MessageId,
                    ctx.Query.Message.Text + $"\n\n{resultText}",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ctx.CancellationToken);
            }
            catch { /* message may be too old to edit */ }
        }

        await AnswerAsync(ctx, approve ? "✅ Одобрен" : "⛔ Отклонён");
    }

    private static async Task AnswerAsync(CallbackContext ctx, string text)
    {
        try { await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, text, cancellationToken: ctx.CancellationToken); }
        catch { }
    }
}
