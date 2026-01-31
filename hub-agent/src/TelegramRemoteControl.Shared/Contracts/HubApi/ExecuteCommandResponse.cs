using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class ExecuteCommandResponse
{
    public bool Success { get; init; }
    public ResponseType Type { get; init; }
    public string? Text { get; init; }
    public string? ErrorMessage { get; init; }
    public byte[]? Data { get; init; }
    public string? FileName { get; init; }
    public string? JsonPayload { get; init; }
    public List<ButtonRow>? Buttons { get; init; }
}
