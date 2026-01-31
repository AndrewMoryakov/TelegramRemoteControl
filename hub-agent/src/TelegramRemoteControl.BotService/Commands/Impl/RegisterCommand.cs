using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.BotService.Menu;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class RegisterCommand : ICommand
{
    public string Id => "register";
    public string[] Aliases => new[] { "/register" };
    public string Title => "Регистрация";
    public string? Icon => "📝";
    public string? Description => "Запросить доступ";
    public string Category => Categories.System;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        await ctx.Bot.SendMessage(ctx.ChatId,
            $"⏳ Заявка на доступ создана.\nВаш ID: `{ctx.UserId}`\n\nОжидайте одобрения администратора.",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            cancellationToken: ctx.CancellationToken);
    }
}
