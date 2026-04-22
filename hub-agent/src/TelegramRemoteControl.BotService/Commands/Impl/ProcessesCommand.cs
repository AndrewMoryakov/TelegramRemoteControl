using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.BotService.Models;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class ProcessesCommand : ProxyCommandBase
{
    public override string Id => "processes";
    public override string[] Aliases => new[] { "/processes", "/proc" };
    public override string Title => "Процессы";
    public override string? Icon => "📋";
    public override string? Description => "Список процессов";
    public override string Category => Categories.Info;

    protected override CommandType AgentCommandType => CommandType.Processes;

    protected override async Task RenderStructured(CommandContext ctx, ExecuteCommandResponse response)
    {
        if (!string.IsNullOrWhiteSpace(ctx.Arguments) && int.TryParse(ctx.Arguments, out var index))
        {
            var agentIdForIndex = (await ctx.Hub.GetSelectedDevice(ctx.UserId))?.AgentId;
            if (!ProcessCache.TryGet(ctx.UserId, agentIdForIndex, out var cached))
            {
                await ctx.ReplyWithMenu("❌ Сначала выполните `/processes`");
                return;
            }

            var info = ProcessListUi.BuildInfo(cached, index);
            if (info == null)
            {
                await ctx.ReplyWithMenu("❌ Неверный номер процесса");
                return;
            }

            await ctx.Bot.SendMessage(ctx.ChatId, info.Value.Text,
                parseMode: ParseMode.Markdown,
                replyMarkup: info.Value.Keyboard,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(response.JsonPayload))
        {
            await ctx.ReplyWithMenu("Нет данных");
            return;
        }

        var processes = JsonSerializer.Deserialize<List<ProcessInfo>>(
            response.JsonPayload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ProcessInfo>();
        if (processes.Count == 0)
        {
            await ctx.ReplyWithMenu("Нет данных");
            return;
        }

        var sorted = ProcessListUi.SortForList(processes);
        var agentIdForList = (await ctx.Hub.GetSelectedDevice(ctx.UserId))?.AgentId;
        ProcessCache.Set(ctx.UserId, agentIdForList, sorted);

        var text = ProcessListUi.BuildList(sorted);
        await ctx.ReplyWithMenu(text);
    }
}
