using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class ExecuteCommandRequest
{
    public long UserId { get; init; }
    public CommandType CommandType { get; init; }
    public string? Arguments { get; init; }
    public Dictionary<string, string>? Parameters { get; init; }
}
