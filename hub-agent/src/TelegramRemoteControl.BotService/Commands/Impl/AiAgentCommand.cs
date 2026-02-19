using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Menu;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class AiAgentCommand : ICommand
{
    public string Id => "ai_agent";
    public string[] Aliases => new[] { "/ai" };
    public string Title => "AI Агент";
    public string? Icon => "🧠";
    public string? Description => "Управление компьютером через AI";
    public string Category => Categories.Ai;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        AiSessionManager.Start(ctx.UserId);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🛑 Выйти из AI", "ai:exit"),
                InlineKeyboardButton.WithCallbackData("🔄 Новая сессия", "ai:new")
            }
        });

        await ctx.Bot.SendMessage(ctx.ChatId,
            "🧠 *AI Агент активирован*\n\n" +
            "Отправьте текстовое сообщение — оно будет выполнено через Claude Code на удаленном ПК.\n\n" +
            "Для выхода нажмите кнопку ниже или отправьте /exit",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            replyMarkup: keyboard,
            cancellationToken: ctx.CancellationToken);
    }
}
