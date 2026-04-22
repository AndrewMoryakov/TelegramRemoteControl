using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.BotService.Models;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class WindowsCommand : ProxyCommandBase
{
    public override string Id => "windows";
    public override string[] Aliases => new[] { "/windows", "/win" };
    public override string Title => "Окна";
    public override string? Icon => "🪟";
    public override string? Description => "Список окон";
    public override string Category => Categories.Screen;

    protected override CommandType AgentCommandType => CommandType.WindowsList;

    protected override async Task RenderResponse(CommandContext ctx, ExecuteCommandResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.JsonPayload))
        {
            await ctx.ReplyWithMenu(response.Text ?? "Нет данных");
            return;
        }

        var windows = JsonSerializer.Deserialize<List<WindowInfo>>(
            response.JsonPayload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<WindowInfo>();

        if (windows.Count == 0)
        {
            await ctx.ReplyWithMenu("🪟 Нет открытых окон");
            return;
        }

        var agentId = (await ctx.Hub.GetSelectedDevice(ctx.UserId))?.AgentId;
        WindowCache.Set(ctx.UserId, agentId, windows);

        var (text, keyboard) = WindowListUi.BuildList(windows);
        await ctx.Bot.SendMessage(ctx.ChatId, text,
            parseMode: ParseMode.Markdown,
            replyMarkup: keyboard,
            cancellationToken: ctx.CancellationToken);
    }
}
