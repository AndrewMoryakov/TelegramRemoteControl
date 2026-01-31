using System.Text.Json;
using Telegram.Bot;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.BotService.Models;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;
using Microsoft.Extensions.Options;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class FilesCommand : ProxyCommandBase
{
    private readonly BotSettings _settings;

    public FilesCommand(IOptions<BotSettings> settings)
    {
        _settings = settings.Value;
    }
    public override string Id => "files";
    public override string[] Aliases => new[] { "/files", "/ls", "/dir" };
    public override string Title => "Файлы";
    public override string? Icon => "📁";
    public override string? Description => "Файловый менеджер";
    public override string Category => Categories.Info;

    protected override CommandType AgentCommandType => CommandType.FileList;

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        // Always show root (drives) on /files
        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.FileList
        });

        if (!response.Success)
        {
            await ctx.ReplyWithMenu($"❌ {response.ErrorMessage}");
            return;
        }

        await RenderResponse(ctx, response);
    }

    protected override async Task RenderResponse(CommandContext ctx, ExecuteCommandResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.JsonPayload))
        {
            await ctx.ReplyWithMenu("Нет данных");
            return;
        }

        var payload = JsonSerializer.Deserialize<FileListPayload>(
            response.JsonPayload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new FileListPayload();

        var session = FileSessionManager.Get(ctx.UserId);
        session.Set(payload.Path, payload.Items);

        if (string.IsNullOrWhiteSpace(payload.Path))
        {
            var (text, keyboard) = FileUi.BuildDrives(session, payload.Items);
            await ctx.Bot.SendMessage(ctx.ChatId, text,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        var (dirText, dirKeyboard) = FileUi.BuildDirectory(session, _settings.FilesPageSize);
        await ctx.Bot.SendMessage(ctx.ChatId, dirText,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            replyMarkup: dirKeyboard,
            cancellationToken: ctx.CancellationToken);
    }
}
