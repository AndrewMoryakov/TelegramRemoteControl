namespace TelegramRemoteControl.BotService;

public class BotSettings
{
    public string Token { get; set; } = string.Empty;
    public long[] AuthorizedUsers { get; set; } = Array.Empty<long>();
    /// <summary>
    /// Optional allowlist for /cmd and /powershell. If empty — only AuthorizedUsers (admins) may use shell.
    /// Any user-id here must also be in AuthorizedUsers or approved via /approve.
    /// </summary>
    public long[] ShellAllowedUsers { get; set; } = Array.Empty<long>();
    /// <summary>
    /// Hard cap on shell command argument length to limit multi-line/huge payloads.
    /// </summary>
    public int ShellMaxArgumentLength { get; set; } = 2000;
    public string HubUrl { get; set; } = "http://localhost:5000";
    public string HubApiKey { get; set; } = string.Empty;
    public int FilesPageSize { get; set; } = 8;
    public int FilesPreviewMaxChars { get; set; } = 3500;
    public int FilesSessionTtlMinutes { get; set; } = 2;
    public bool DeviceStatusMonitorEnabled { get; set; } = false;
    public int DeviceStatusMonitorIntervalSeconds { get; set; } = 10;
    public bool HubHealthMonitorEnabled { get; set; } = true;
    public int HubHealthMonitorIntervalSeconds { get; set; } = 300;
}
