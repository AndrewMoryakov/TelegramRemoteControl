namespace TelegramRemoteControl.Shared.Protocol;

public class AgentInfo
{
    public string AgentId { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string? FriendlyName { get; init; }
    public string? OsVersion { get; init; }
    public string? UserName { get; init; }
}
