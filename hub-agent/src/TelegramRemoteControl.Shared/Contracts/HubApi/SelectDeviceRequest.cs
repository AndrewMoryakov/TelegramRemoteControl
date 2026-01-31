namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class SelectDeviceRequest
{
    public long UserId { get; init; }
    public string AgentId { get; init; } = string.Empty;
}
