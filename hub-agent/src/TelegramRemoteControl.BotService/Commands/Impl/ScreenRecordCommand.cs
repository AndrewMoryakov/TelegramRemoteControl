using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class ScreenRecordCommand : ICommand
{
    public string Id => "record";
    public string[] Aliases => new[] { "/record" };
    public string Title => "Запись экрана";
    public string? Icon => "🎬";
    public string? Description => "Запись экрана (ZIP с кадрами)";
    public string Category => Categories.Screen;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var seconds = 5;
        if (!string.IsNullOrWhiteSpace(ctx.Arguments) &&
            int.TryParse(ctx.Arguments.Trim(), out var parsed))
        {
            seconds = Math.Clamp(parsed, 1, 30);
        }

        await ctx.Bot.SendChatAction(ctx.ChatId, ChatAction.UploadDocument, cancellationToken: ctx.CancellationToken);

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = CommandType.ScreenRecord,
            Arguments = seconds.ToString()
        });

        if (!response.Success)
        {
            await ctx.ReplyWithMenu($"❌ {response.ErrorMessage}");
            return;
        }

        if (response.Data == null || response.Data.Length == 0)
        {
            await ctx.ReplyWithMenu("❌ Пустой ответ от агента");
            return;
        }

        await using var stream = new MemoryStream(response.Data);
        await ctx.Bot.SendDocument(ctx.ChatId,
            Telegram.Bot.Types.InputFile.FromStream(stream, response.FileName ?? "recording.zip"),
            caption: response.Text ?? $"Запись экрана {seconds}с",
            cancellationToken: ctx.CancellationToken);
    }
}
