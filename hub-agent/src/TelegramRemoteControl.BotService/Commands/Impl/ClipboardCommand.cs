using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class ClipboardCommand : ICommand
{
    public string Id => "clip";
    public string[] Aliases => new[] { "/clip" };
    public string Title => "Буфер обмена";
    public string? Icon => "📋";
    public string? Description => "Просмотр/изменение буфера обмена";
    public string Category => Categories.System;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Arguments))
        {
            // GET
            var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
            {
                UserId = ctx.UserId,
                CommandType = CommandType.ClipboardGet
            });

            if (!response.Success)
                await ctx.ReplyWithMenu($"❌ {response.ErrorMessage}");
            else
                await ctx.ReplyWithMenu(response.Text ?? "(буфер обмена пуст)");
        }
        else
        {
            // SET
            var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
            {
                UserId = ctx.UserId,
                CommandType = CommandType.ClipboardSet,
                Arguments = ctx.Arguments
            });

            if (!response.Success)
                await ctx.ReplyWithMenu($"❌ {response.ErrorMessage}");
            else
                await ctx.ReplyWithMenu(response.Text ?? "✅ Готово");
        }
    }
}
