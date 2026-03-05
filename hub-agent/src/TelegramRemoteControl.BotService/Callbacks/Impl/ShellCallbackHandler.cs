using Telegram.Bot;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class ShellCallbackHandler : ICallbackHandler
{
    private readonly Menu.MenuBuilder _menu;
    private readonly HubClient _hubClient;

    public ShellCallbackHandler(Menu.MenuBuilder menu, HubClient hubClient)
    {
        _menu = menu;
        _hubClient = hubClient;
    }

    public string Prefix => "shell";

    public async Task HandleAsync(CallbackContext ctx)
    {
        var action = ctx.Data.Length > "shell:".Length ? ctx.Data["shell:".Length..] : string.Empty;

        if (action != "exit")
        {
            try { await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, cancellationToken: ctx.CancellationToken); } catch { }
            return;
        }

        ShellSessionManager.End(ctx.UserId);

        var selected = await _hubClient.GetSelectedDevice(ctx.UserId);
        var name = selected == null ? null : selected.FriendlyName ?? selected.MachineName ?? selected.AgentId;

        await ctx.Bot.SendMessage(ctx.ChatId,
            "🤖 Пульт управления\n\nВыберите действие:",
            replyMarkup: _menu.MainMenu(name),
            cancellationToken: ctx.CancellationToken);

        try { await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, cancellationToken: ctx.CancellationToken); } catch { }
    }
}
