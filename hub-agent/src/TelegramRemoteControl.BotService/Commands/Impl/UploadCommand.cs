using TelegramRemoteControl.BotService.Menu;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class UploadCommand : ICommand
{
    public string Id => "upload";
    public string[] Aliases => new[] { "/upload" };
    public string Title => "Загрузить файл";
    public string? Icon => "📤";
    public string? Description => "Загрузить файл на ПК";
    public string Category => Categories.System;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var dest = string.IsNullOrWhiteSpace(ctx.Arguments) ? null : ctx.Arguments.Trim();
        var displayPath = dest ?? "Downloads";

        FileUploadSession.Start(ctx.UserId, dest);

        await ctx.ReplyWithBack($"📤 Отправьте файл для загрузки на ПК\nПапка назначения: `{displayPath}`\n\nОтправьте /cancel для отмены.");
    }
}
