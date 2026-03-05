using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace TelegramRemoteControl.BotService.Services;

public class HubHealthMonitor : BackgroundService
{
    private readonly HubClient _hubClient;
    private readonly ITelegramBotClient _bot;
    private readonly BotSettings _settings;
    private readonly ILogger<HubHealthMonitor> _logger;

    private bool _lastKnownAlive = true;

    public HubHealthMonitor(HubClient hubClient, ITelegramBotClient bot, BotSettings settings, ILogger<HubHealthMonitor> logger)
    {
        _hubClient = hubClient;
        _bot = bot;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.HubHealthMonitorEnabled || _settings.AuthorizedUsers.Length == 0)
            return;

        _logger.LogInformation("HubHealthMonitor started, interval={Interval}s", _settings.HubHealthMonitorIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.HubHealthMonitorIntervalSeconds), stoppingToken);

                var alive = await _hubClient.IsHubAlive();

                if (!alive && _lastKnownAlive)
                {
                    _lastKnownAlive = false;
                    _logger.LogWarning("Hub is DOWN");
                    await NotifyAdminsAsync("🔴 *Hub недоступен*\n\nНет ответа от Hub-сервера.", stoppingToken);
                }
                else if (alive && !_lastKnownAlive)
                {
                    _lastKnownAlive = true;
                    _logger.LogInformation("Hub recovered");
                    await NotifyAdminsAsync("🟢 *Hub восстановлен*\n\nСоединение с Hub-сервером восстановлено.", stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HubHealthMonitor error");
            }
        }
    }

    private async Task NotifyAdminsAsync(string text, CancellationToken ct)
    {
        foreach (var adminId in _settings.AuthorizedUsers)
        {
            try
            {
                await _bot.SendMessage(adminId, text, parseMode: ParseMode.Markdown, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify admin {AdminId}", adminId);
            }
        }
    }
}
