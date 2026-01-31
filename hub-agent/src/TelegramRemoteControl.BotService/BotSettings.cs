namespace TelegramRemoteControl.BotService;

public class BotSettings
{
    public string Token { get; set; } = string.Empty;
    public long[] AuthorizedUsers { get; set; } = Array.Empty<long>();
    public string HubUrl { get; set; } = "http://localhost:5000";
    public string HubApiKey { get; set; } = string.Empty;
    public int FilesPageSize { get; set; } = 8;
    public int FilesPreviewMaxChars { get; set; } = 3500;
    public int FilesSessionTtlMinutes { get; set; } = 2;
    public bool DeviceStatusMonitorEnabled { get; set; } = false;
    public int DeviceStatusMonitorIntervalSeconds { get; set; } = 10;
}
