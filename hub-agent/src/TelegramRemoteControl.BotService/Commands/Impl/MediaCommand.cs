using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Menu;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class MediaCommand : ICommand
{
    public string Id => "media";
    public string[] Aliases => new[] { "/media" };
    public string Title => "Медиа";
    public string? Icon => "🎵";
    public string? Description => "Управление медиаплеером";
    public string Category => Categories.Control;

    public Task ExecuteAsync(CommandContext ctx)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⏮ Пред.", "media:prev"),
                InlineKeyboardButton.WithCallbackData("⏯ Пауза", "media:play"),
                InlineKeyboardButton.WithCallbackData("⏭ След.", "media:next"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔉 Тише", "media:voldown"),
                InlineKeyboardButton.WithCallbackData("🔇 Без звука", "media:mute"),
                InlineKeyboardButton.WithCallbackData("🔊 Громче", "media:volup"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu")
            }
        });

        return ctx.Bot.SendMessage(ctx.ChatId, "🎵 Управление медиаплеером:",
            replyMarkup: keyboard,
            cancellationToken: ctx.CancellationToken);
    }
}
