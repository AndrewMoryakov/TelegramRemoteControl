using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands;

public abstract class ProxyCommandBase : ICommand
{
    public abstract string Id { get; }
    public abstract string[] Aliases { get; }
    public abstract string Title { get; }
    public abstract string? Icon { get; }
    public abstract string? Description { get; }
    public abstract string Category { get; }
    protected abstract CommandType AgentCommandType { get; }

    public virtual async Task ExecuteAsync(CommandContext ctx)
    {
        if (!ValidateArguments(ctx, out var errorMessage))
        {
            await ctx.ReplyWithBack(errorMessage ?? "❌ Некорректные параметры");
            return;
        }

        var response = await ctx.Hub.ExecuteCommand(new ExecuteCommandRequest
        {
            UserId = ctx.UserId,
            CommandType = AgentCommandType,
            Arguments = ctx.Arguments
        });

        if (!response.Success)
        {
            await ctx.ReplyWithMenu($"❌ {response.ErrorMessage}");
            return;
        }

        await RenderResponse(ctx, response);
    }

    protected virtual bool ValidateArguments(CommandContext ctx, out string? errorMessage)
    {
        errorMessage = null;
        return true;
    }

    protected virtual Task RenderResponse(CommandContext ctx, ExecuteCommandResponse response)
    {
        return response.Type switch
        {
            ResponseType.Text => ctx.ReplyWithMenu(response.Text ?? "Нет данных"),
            ResponseType.Photo => SendPhotoAsync(ctx, response),
            ResponseType.Document => SendDocumentAsync(ctx, response),
            ResponseType.Structured => RenderStructured(ctx, response),
            ResponseType.Error => ctx.ReplyWithMenu($"❌ {response.ErrorMessage ?? "Ошибка"}"),
            _ => ctx.ReplyWithMenu("❌ Неизвестный тип ответа от агента")
        };
    }

    protected virtual Task RenderStructured(CommandContext ctx, ExecuteCommandResponse response)
    {
        var text = response.Text ?? response.JsonPayload ?? "Нет данных";
        return ctx.ReplyWithMenu(text);
    }

    private static async Task SendPhotoAsync(CommandContext ctx, ExecuteCommandResponse response)
    {
        if (response.Data == null || response.Data.Length == 0)
        {
            await ctx.ReplyWithMenu("❌ Пустой ответ от агента");
            return;
        }

        await using var stream = new MemoryStream(response.Data);
        await ctx.Bot.SendPhoto(ctx.ChatId,
            InputFile.FromStream(stream, response.FileName ?? "image.png"),
            cancellationToken: ctx.CancellationToken);
    }

    private static async Task SendDocumentAsync(CommandContext ctx, ExecuteCommandResponse response)
    {
        if (response.Data == null || response.Data.Length == 0)
        {
            await ctx.ReplyWithMenu("❌ Пустой ответ от агента");
            return;
        }

        await using var stream = new MemoryStream(response.Data);
        await ctx.Bot.SendDocument(ctx.ChatId,
            InputFile.FromStream(stream, response.FileName ?? "file.bin"),
            cancellationToken: ctx.CancellationToken);
    }
}
