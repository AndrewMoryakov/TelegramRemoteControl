using Microsoft.Extensions.Options;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Contracts.HubApi;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class ApproveCommand : ICommand
{
    private readonly BotSettings _settings;

    public ApproveCommand(IOptions<BotSettings> settings)
    {
        _settings = settings.Value;
    }

    public string Id => "approve";
    public string[] Aliases => new[] { "/approve" };
    public string Title => "Одобрить";
    public string? Icon => "✅";
    public string? Description => "Разрешить доступ пользователю";
    public string Category => Categories.System;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (!IsAdmin(ctx.UserId))
        {
            await ctx.ReplyWithBack("⛔ Нет доступа");
            return;
        }

        if (!TryParseUserId(ctx.Arguments, out var userId))
        {
            await ctx.ReplyWithBack("❌ Укажите userId: `/approve 123456`");
            return;
        }

        await ctx.Hub.SetUserAuthorized(new UserAuthorizeRequest
        {
            UserId = userId,
            Authorized = true
        });

        await ctx.ReplyWithBack($"✅ Пользователь {userId} одобрен");
    }

    private bool IsAdmin(long userId)
    {
        return _settings.AuthorizedUsers.Length > 0 && _settings.AuthorizedUsers.Contains(userId);
    }

    private static bool TryParseUserId(string? text, out long userId)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            userId = 0;
            return false;
        }

        return long.TryParse(text.Trim(), out userId);
    }
}
