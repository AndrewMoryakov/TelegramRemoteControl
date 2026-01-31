using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace TelegramRemoteControl.BotService;

public class TelegramBotService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly BotHandler _handler;
    private readonly ILogger<TelegramBotService> _logger;

    public TelegramBotService(ITelegramBotClient bot, BotHandler handler, ILogger<TelegramBotService> logger)
    {
        _bot = bot;
        _handler = handler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telegram bot starting...");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<Telegram.Bot.Types.Enums.UpdateType>()
        };

        _bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            stoppingToken);

        var me = await _bot.GetMe(stoppingToken);
        _logger.LogInformation("Bot started: @{BotName}", me.Username);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message != null)
        {
            await _handler.HandleMessageAsync(bot, update.Message, ct);
        }
        else if (update.CallbackQuery != null)
        {
            await _handler.HandleCallbackAsync(bot, update.CallbackQuery, ct);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram bot error");
        return Task.CompletedTask;
    }
}
