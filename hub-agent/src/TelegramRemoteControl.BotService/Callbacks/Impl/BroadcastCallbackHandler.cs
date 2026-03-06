using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Callbacks.Impl;

public class BroadcastCallbackHandler : ICallbackHandler
{
    public string Prefix => "bcast";

    private static readonly Dictionary<string, (CommandType Type, string Label)> Actions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["status"]     = (CommandType.Status,     "Статус"),
            ["screenshot"] = (CommandType.Screenshot,  "Скриншот"),
            ["lock"]       = (CommandType.Lock,        "Блокировка"),
            ["sleep"]      = (CommandType.Sleep,       "Сон"),
        };

    public async Task HandleAsync(CallbackContext ctx)
    {
        var action = ctx.Data.Length > "bcast:".Length ? ctx.Data["bcast:".Length..] : string.Empty;

        if (!Actions.TryGetValue(action, out var cmd))
        {
            try { await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, "Неизвестная команда", cancellationToken: ctx.CancellationToken); } catch { }
            return;
        }

        try
        {
            await ctx.Bot.AnswerCallbackQuery(ctx.Query.Id, $"📡 {cmd.Label} → все устройства...", cancellationToken: ctx.CancellationToken);
        }
        catch { }

        var results = await ctx.Hub.BroadcastCommand(new BroadcastRequest
        {
            UserId = ctx.UserId,
            CommandType = cmd.Type
        });

        if (results.Count == 0)
        {
            await ctx.Bot.SendMessage(ctx.ChatId,
                "📡 Нет онлайн-устройств",
                cancellationToken: ctx.CancellationToken);
            return;
        }

        foreach (var result in results)
        {
            var name = result.FriendlyName ?? result.MachineName;

            if (!result.Success)
            {
                await ctx.Bot.SendMessage(ctx.ChatId,
                    $"🔴 *{name}*: {result.ErrorMessage ?? "Ошибка"}",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    cancellationToken: ctx.CancellationToken);
                continue;
            }

            if (result.Data != null && result.Data.Length > 0 && cmd.Type == CommandType.Screenshot)
            {
                await using var stream = new MemoryStream(result.Data);
                await ctx.Bot.SendPhoto(ctx.ChatId,
                    InputFile.FromStream(stream, $"{name}.png"),
                    caption: $"📸 *{name}*",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    cancellationToken: ctx.CancellationToken);
            }
            else
            {
                var text = result.Text ?? "✅ Готово";
                await ctx.Bot.SendMessage(ctx.ChatId,
                    $"🟢 *{name}*:\n{text}",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    cancellationToken: ctx.CancellationToken);
            }
        }
    }
}
