namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class DeviceDto
{
    public string AgentId { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string? FriendlyName { get; init; }
    public bool IsOnline { get; init; }
    public DateTime? LastSeen { get; init; }
}
