using System.Text.Json;
using Telegram.Bot;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.BotService.Models;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class ServicesCommand : ProxyCommandBase
{
    public override string Id => "services";
    public override string[] Aliases => new[] { "/services", "/svc" };
    public override string Title => "Службы";
    public override string? Icon => "⚙️";
    public override string? Description => "Управление службами";
    public override string Category => Categories.Control;

    protected override CommandType AgentCommandType => CommandType.Services;

    protected override async Task RenderStructured(CommandContext ctx, ExecuteCommandResponse response)
    {
        if (!string.IsNullOrWhiteSpace(ctx.Arguments))
        {
            if (!ServiceCache.TryGet(ctx.UserId, out var cached))
            {
                await ctx.ReplyWithBack("❌ Сначала выполните `/services`");
                return;
            }

            var info = FindService(cached, ctx.Arguments.Trim());
            if (info == null)
            {
                await ctx.ReplyWithBack($"❌ Служба не найдена: `{ctx.Arguments}`");
                return;
            }

            var detail = ServiceUi.BuildInfo(info);
            await ctx.Bot.SendMessage(ctx.ChatId, detail.Text,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: detail.Keyboard,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(response.JsonPayload))
        {
            await ctx.ReplyWithMenu("Нет данных");
            return;
        }

        var services = JsonSerializer.Deserialize<List<ServiceInfo>>(
            response.JsonPayload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ServiceInfo>();

        if (services.Count == 0)
        {
            await ctx.ReplyWithMenu("Нет данных");
            return;
        }

        ServiceCache.Set(ctx.UserId, services);
        var text = ServiceUi.BuildListText(services);
        await ctx.ReplyWithBack(text);
    }

    private static ServiceInfo? FindService(List<ServiceInfo> services, string name)
    {
        return services.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
