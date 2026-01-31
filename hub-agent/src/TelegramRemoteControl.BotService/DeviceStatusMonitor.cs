using Telegram.Bot;
using TelegramRemoteControl.Shared.Contracts.HubApi;

namespace TelegramRemoteControl.BotService;

public class DeviceStatusMonitor : BackgroundService
{
    private readonly HubClient _hub;
    private readonly ITelegramBotClient _bot;
    private readonly BotSettings _settings;
    private readonly ILogger<DeviceStatusMonitor> _logger;
    private readonly Dictionary<string, bool> _lastStatus = new();

    public DeviceStatusMonitor(
        HubClient hub,
        ITelegramBotClient bot,
        BotSettings settings,
        ILogger<DeviceStatusMonitor> logger)
    {
        _hub = hub;
        _bot = bot;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.DeviceStatusMonitorEnabled)
        {
            _logger.LogInformation("DeviceStatusMonitor disabled");
            return;
        }

        var intervalSeconds = _settings.DeviceStatusMonitorIntervalSeconds <= 0
            ? 10
            : _settings.DeviceStatusMonitorIntervalSeconds;
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        _logger.LogInformation("DeviceStatusMonitor started. Interval={IntervalSeconds}s", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeviceStatusMonitor poll failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var users = await _hub.GetNotifiedUsers();
        if (users.Count == 0)
        {
            _logger.LogDebug("DeviceStatusMonitor: no subscribed users");
            return;
        }

        foreach (var userId in users.Distinct())
        {
            DeviceListResponse devices;
            try
            {
                devices = await _hub.GetDevices(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch devices for user {UserId}", userId);
                continue;
            }

            foreach (var device in devices.Devices)
            {
                var key = $"{userId}:{device.AgentId}";
                if (!_lastStatus.TryGetValue(key, out var prev))
                {
                    _lastStatus[key] = device.IsOnline;
                    continue;
                }

                if (prev == device.IsOnline)
                    continue;

                _lastStatus[key] = device.IsOnline;
                await SendStatusAsync(userId, device, device.IsOnline, ct);
            }
        }

    }

    private async Task SendStatusAsync(long userId, DeviceDto device, bool isOnline, CancellationToken ct)
    {
        var name = GetDeviceName(device);
        var text = isOnline
            ? $"🟢 {name} подключился"
            : $"🔴 {name} отключился";

        try
        {
            await _bot.SendMessage(userId, text, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send status notification to user {UserId}", userId);
        }
    }

    private static string GetDeviceName(DeviceDto device)
    {
        return device.FriendlyName ?? device.MachineName ?? device.AgentId;
    }
}
