namespace TelegramRemoteControl.Shared.Protocol;

public class AgentResponse
{
    public string RequestId { get; init; } = string.Empty;
    public ResponseType Type { get; init; }
    public bool Success { get; init; }
    public string? Text { get; init; }
    public string? ErrorMessage { get; init; }
    public byte[]? Data { get; init; }
    public string? FileName { get; init; }
    public string? JsonPayload { get; init; }
    public List<ButtonRow>? Buttons { get; init; }
}
