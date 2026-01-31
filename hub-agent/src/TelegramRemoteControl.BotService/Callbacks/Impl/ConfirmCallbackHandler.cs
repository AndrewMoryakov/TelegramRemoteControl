using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.BotService.Commands;
using TelegramRemoteControl.BotService.Menu;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class ConfirmCallbackHandler : ICallbackHandler
{
    private readonly CommandRegistry _commands;
    private readonly MenuBuilder _menu;

    public ConfirmCallbackHandler(CommandRegistry commands, MenuBuilder menu)
    {
        _commands = commands;
        _menu = menu;
    }

    public string Prefix => "confirm";

    public async Task HandleAsync(CallbackContext ctx)
    {
        var payload = ctx.Data.Length > "confirm:".Length
            ? ctx.Data["confirm:".Length..]
            : string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
            return;

        string commandId;
        string? arguments = null;
        var separatorIndex = payload.IndexOf(':');
        if (separatorIndex < 0)
        {
            commandId = payload;
        }
        else
        {
            commandId = payload[..separatorIndex];
            arguments = payload[(separatorIndex + 1)..];
        }
        var command = _commands.FindById(commandId);
        if (command == null)
            return;

        var commandCtx = CreateCommandContext(ctx, arguments);

        if (command is IConfirmableCommand confirmable)
        {
            await confirmable.ExecuteConfirmedAsync(commandCtx);
        }
        else
        {
            await command.ExecuteAsync(commandCtx);
        }

        if (ctx.MessageId.HasValue)
        {
            try
            {
                await ctx.Bot.DeleteMessage(ctx.ChatId, ctx.MessageId.Value, ctx.CancellationToken);
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, cancellationToken: ctx.CancellationToken);
        }
        catch
        {
            // ignore expired callback
        }
    }

    private CommandContext CreateCommandContext(CallbackContext ctx, string? arguments)
    {
        return new CommandContext
        {
            Bot = ctx.Bot,
            ChatId = ctx.ChatId,
            UserId = ctx.UserId,
            Arguments = arguments,
            Hub = ctx.Hub,
            CancellationToken = ctx.CancellationToken,
            ReplyWithMenu = async text =>
            {
                var menu = await BuildMainMenuAsync(ctx.UserId, ctx);
                var parseMode = GetParseMode(text);
                if (parseMode.HasValue)
                {
                    await ctx.Bot.SendMessage(ctx.ChatId, text,
                        parseMode: parseMode.Value,
                        replyMarkup: menu,
                        cancellationToken: ctx.CancellationToken);
                }
                else
                {
                    await ctx.Bot.SendMessage(ctx.ChatId, text,
                        replyMarkup: menu,
                        cancellationToken: ctx.CancellationToken);
                }
            },
            ReplyWithBack = text =>
            {
                var parseMode = GetParseMode(text);
                InlineKeyboardMarkup back = _menu.BackButton();
                if (parseMode.HasValue)
                {
                    return ctx.Bot.SendMessage(ctx.ChatId, text,
                        parseMode: parseMode.Value,
                        replyMarkup: back,
                        cancellationToken: ctx.CancellationToken);
                }

                return ctx.Bot.SendMessage(ctx.ChatId, text,
                    replyMarkup: back,
                    cancellationToken: ctx.CancellationToken);
            }
        };
    }

    private async Task<InlineKeyboardMarkup> BuildMainMenuAsync(long userId, CallbackContext ctx)
    {
        var selected = await ctx.Hub.GetSelectedDevice(userId);
        var name = selected == null ? null : GetDeviceName(selected);
        return _menu.MainMenu(name);
    }

    private static string GetDeviceName(TelegramRemoteControl.Shared.Contracts.HubApi.DeviceDto device)
    {
        return device.FriendlyName ?? device.MachineName ?? device.AgentId;
    }

    private static ParseMode? GetParseMode(string text)
    {
        return text.Contains("```", StringComparison.Ordinal) ? ParseMode.Markdown : null;
    }

}
