namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class BroadcastResultDto
{
    public string AgentId { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string? FriendlyName { get; init; }
    public bool Success { get; init; }
    public string? Text { get; init; }
    public string? ErrorMessage { get; init; }
    public byte[]? Data { get; init; }
}
