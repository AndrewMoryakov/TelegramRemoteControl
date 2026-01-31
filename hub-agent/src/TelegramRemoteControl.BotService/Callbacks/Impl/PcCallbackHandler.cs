using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Menu;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class PcCallbackHandler : ICallbackHandler
{
    private readonly MenuBuilder _menu;

    public PcCallbackHandler(MenuBuilder menu)
    {
        _menu = menu;
    }

    public string Prefix => "pc";

    public async Task HandleAsync(CallbackContext ctx)
    {
        var parts = ctx.Data.Split(':');
        if (parts.Length < 2)
            return;

        if (parts[1] == "list")
        {
            await ShowDeviceListAsync(ctx);
            return;
        }

        if (parts.Length < 3 || parts[1] != "select")
            return;

        var agentId = parts[2];

        try
        {
            await ctx.Hub.SelectDevice(ctx.UserId, agentId);
            var selected = await ctx.Hub.GetSelectedDevice(ctx.UserId);
            var name = selected?.FriendlyName ?? selected?.MachineName ?? agentId;

            await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id,
                $"✅ Выбран: {name}", cancellationToken: ctx.CancellationToken);

            if (ctx.MessageId.HasValue)
            {
                var menu = _menu.MainMenu(name);
                await ctx.Bot.EditMessageText(ctx.ChatId, ctx.MessageId.Value,
                    "🤖 Пульт управления\n\nВыберите действие:",
                    replyMarkup: menu,
                    cancellationToken: ctx.CancellationToken);
            }
            else
            {
                var menu = _menu.MainMenu(name);
                await ctx.Bot.SendMessage(ctx.ChatId,
                    "🤖 Пульт управления\n\nВыберите действие:",
                    replyMarkup: menu,
                    cancellationToken: ctx.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id,
                $"❌ Ошибка: {ex.Message}", showAlert: true,
                cancellationToken: ctx.CancellationToken);
        }
    }

    private async Task ShowDeviceListAsync(CallbackContext ctx)
    {
        try
        {
            var devices = await ctx.Hub.GetDevices(ctx.UserId);

            if (devices.Devices.Count == 0)
            {
            try
            {
                await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, "Нет привязанных устройств", showAlert: true,
                    cancellationToken: ctx.CancellationToken);
            }
            catch
            {
                // ignore expired callback
            }
            return;
        }

            var buttons = devices.Devices.Select(d =>
                new[] { InlineKeyboardButton.WithCallbackData(
                    $"{(d.IsOnline ? "🟢" : "🔴")} {d.FriendlyName ?? d.MachineName}",
                    $"pc:select:{d.AgentId}") }
            ).ToArray();

            if (ctx.MessageId.HasValue)
            {
                await ctx.Bot.EditMessageText(ctx.ChatId, ctx.MessageId.Value,
                    "🖥 Выберите компьютер:",
                    replyMarkup: new InlineKeyboardMarkup(buttons),
                    cancellationToken: ctx.CancellationToken);
            }
            else
            {
                await ctx.Bot.SendMessage(ctx.ChatId,
                    "🖥 Выберите компьютер:",
                    replyMarkup: new InlineKeyboardMarkup(buttons),
                    cancellationToken: ctx.CancellationToken);
            }

            try
            {
                await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, cancellationToken: ctx.CancellationToken);
            }
            catch
            {
                // ignore expired callback
            }
        }
        catch (Exception ex)
        {
            try
            {
                await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id,
                    $"❌ Ошибка: {ex.Message}", showAlert: true,
                    cancellationToken: ctx.CancellationToken);
            }
            catch
            {
                // ignore expired callback
            }
        }
    }
}
