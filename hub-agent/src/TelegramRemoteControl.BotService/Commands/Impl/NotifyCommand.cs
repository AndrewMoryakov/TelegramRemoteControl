using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Contracts.HubApi;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class NotifyCommand : ICommand
{
    public string Id => "notify";
    public string[] Aliases => new[] { "/notify", "/notifications" };
    public string Title => "Уведомления";
    public string? Icon => "🔔";
    public string? Description => "Управление уведомлениями";
    public string Category => Categories.System;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var arg = ctx.Arguments?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(arg) || IsStatus(arg))
        {
            var enabled = await ctx.Hub.GetUserNotifyStatus(ctx.UserId);
            var status = enabled ? "включены" : "выключены";
            var text = $"🔔 Уведомления сейчас {status}.";
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ Включить", "notify:on"),
                    InlineKeyboardButton.WithCallbackData("❌ Выключить", "notify:off")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu")
                }
            });

            await ctx.Bot.SendMessage(ctx.ChatId, text,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        if (IsOn(arg))
        {
            await ctx.Hub.SetUserNotify(new UserNotifyRequest { UserId = ctx.UserId, Enabled = true });
            await ctx.ReplyWithBack("✅ Уведомления включены");
            return;
        }

        if (IsOff(arg))
        {
            await ctx.Hub.SetUserNotify(new UserNotifyRequest { UserId = ctx.UserId, Enabled = false });
            await ctx.ReplyWithBack("✅ Уведомления выключены");
            return;
        }

        await ctx.ReplyWithBack("❌ Используйте: `/notify on` или `/notify off`");
    }

    private static bool IsOn(string arg)
    {
        return arg is "on" or "true" or "1" or "enable" or "start";
    }

    private static bool IsOff(string arg)
    {
        return arg is "off" or "false" or "0" or "disable" or "stop";
    }

    private static bool IsStatus(string arg)
    {
        return arg is "status" or "state" or "show";
    }
}
