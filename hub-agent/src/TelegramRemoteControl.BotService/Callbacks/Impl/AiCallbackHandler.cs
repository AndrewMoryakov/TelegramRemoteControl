using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Menu;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class AiCallbackHandler : ICallbackHandler
{
    private readonly MenuBuilder _menu;
    private readonly HubClient _hubClient;

    public AiCallbackHandler(MenuBuilder menu, HubClient hubClient)
    {
        _menu = menu;
        _hubClient = hubClient;
    }

    public string Prefix => "ai";

    public async Task HandleAsync(CallbackContext ctx)
    {
        var action = ctx.Data.Length > "ai:".Length ? ctx.Data["ai:".Length..] : string.Empty;

        switch (action)
        {
            case "exit":
                AiSessionManager.End(ctx.UserId);
                var selected = await _hubClient.GetSelectedDevice(ctx.UserId);
                var name = selected == null ? null : selected.FriendlyName ?? selected.MachineName ?? selected.AgentId;
                var menu = _menu.MainMenu(name);

                await ctx.Bot.SendMessage(ctx.ChatId,
                    "🤖 Пульт управления\n\nВыберите действие:",
                    replyMarkup: menu,
                    cancellationToken: ctx.CancellationToken);

                try { await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, cancellationToken: ctx.CancellationToken); } catch { }
                break;

            case "new":
                var session = AiSessionManager.Get(ctx.UserId);
                if (session != null)
                {
                    session.ClaudeSessionId = null;
                    session.MessageCount = 0;
                    session.LastMessageAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    AiSessionManager.Start(ctx.UserId);
                }

                await ctx.Bot.SendMessage(ctx.ChatId,
                    "🔄 Новая AI сессия начата. Отправьте сообщение.",
                    replyMarkup: AiModeKeyboard(),
                    cancellationToken: ctx.CancellationToken);

                try { await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, "Новая сессия", cancellationToken: ctx.CancellationToken); } catch { }
                break;

            default:
                try { await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, cancellationToken: ctx.CancellationToken); } catch { }
                break;
        }
    }

    private static InlineKeyboardMarkup AiModeKeyboard() => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("🛑 Выйти из AI", "ai:exit"),
            InlineKeyboardButton.WithCallbackData("🔄 Новая сессия", "ai:new")
        }
    });
}
