using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class BroadcastRequest
{
    public long UserId { get; init; }
    public CommandType CommandType { get; init; }
    public string? Arguments { get; init; }
}
