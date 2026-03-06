using Telegram.Bot;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class MediaCallbackHandler : ICallbackHandler
{
    public string Prefix => "media";

    public async Task HandleAsync(CallbackContext ctx)
    {
        var action = ctx.Data.Length > "media:".Length ? ctx.Data["media:".Length..] : string.Empty;

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.Media,
            Arguments = action
        });

        var text = response.Success
            ? response.Text ?? "✅ Готово"
            : response.ErrorMessage ?? "❌ Ошибка";

        try
        {
            await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, text, cancellationToken: ctx.CancellationToken);
        }
        catch { }
    }
}
