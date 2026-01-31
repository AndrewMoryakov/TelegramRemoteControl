using Telegram.Bot;
using TelegramRemoteControl.BotService.Menu;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class SelectPcCommand : ICommand
{
    public string Id => "pc";
    public string[] Aliases => new[] { "/pc", "/devices" };
    public string Title => "Выбрать ПК";
    public string? Icon => "🖥";
    public string? Description => "Список и выбор компьютера";
    public string Category => Categories.System;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        try
        {
            var devices = await ctx.Hub.GetDevices(ctx.UserId);

            if (devices.Devices.Count == 0)
            {
                await ctx.Bot.SendMessage(ctx.ChatId,
                    "Нет привязанных устройств.\nИспользуйте /addpc для привязки.",
                    cancellationToken: ctx.CancellationToken);
                return;
            }

            var buttons = devices.Devices.Select(d =>
                new[] { InlineKeyboardButton.WithCallbackData(
                    $"{(d.IsOnline ? "🟢" : "🔴")} {d.FriendlyName ?? d.MachineName}",
                    $"pc:select:{d.AgentId}") }
            ).ToArray();

            await ctx.Bot.SendMessage(ctx.ChatId,
                "🖥 Выберите компьютер:",
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: ctx.CancellationToken);
        }
        catch (Exception ex)
        {
            await ctx.Bot.SendMessage(ctx.ChatId,
                $"❌ Ошибка: {ex.Message}",
                cancellationToken: ctx.CancellationToken);
        }
    }
}
