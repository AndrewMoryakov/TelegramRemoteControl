using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Menu;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class BroadcastCommand : ICommand
{
    public string Id => "broadcast";
    public string[] Aliases => new[] { "/broadcast" };
    public string Title => "Широковещание";
    public string? Icon => "📡";
    public string? Description => "Команда всем устройствам";
    public string Category => Categories.System;

    public Task ExecuteAsync(CommandContext ctx)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📊 Статус", "bcast:status"),
                InlineKeyboardButton.WithCallbackData("📸 Скриншот", "bcast:screenshot"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔒 Блокировать", "bcast:lock"),
                InlineKeyboardButton.WithCallbackData("💤 Сон", "bcast:sleep"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu")
            }
        });

        return ctx.Bot.SendMessage(ctx.ChatId,
            "📡 Выберите команду для всех онлайн-устройств:",
            replyMarkup: keyboard,
            cancellationToken: ctx.CancellationToken);
    }
}
