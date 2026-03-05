using Telegram.Bot;
using TelegramRemoteControl.BotService.Menu;
using Telegram.Bot.Types.Enums;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class AddPcCommand : ICommand
{
    public string Id => "addpc";
    public string[] Aliases => new[] { "/addpc" };
    public string Title => "Добавить ПК";
    public string? Icon => "➕";
    public string? Description => "Привязать новый компьютер";
    public string Category => Categories.System;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        try
        {
            var result = await ctx.Hub.GeneratePairCode(ctx.UserId);
            await ctx.Bot.SendMessage(ctx.ChatId,
                $"🔗 Код привязки: `{result.Code}`\n\n" +
                "Введите этот код в `appsettings.json` агента в поле `PairingCode`.\n" +
                "Код действителен 6 месяцев.",
                parseMode: ParseMode.Markdown,
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
