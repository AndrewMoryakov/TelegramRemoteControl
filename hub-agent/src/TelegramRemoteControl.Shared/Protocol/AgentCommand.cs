namespace TelegramRemoteControl.Shared.Protocol;

public class AgentCommand
{
    public string RequestId { get; init; } = string.Empty;
    public CommandType Type { get; init; }
    public string? Arguments { get; init; }
    public Dictionary<string, string>? Parameters { get; init; }
}
