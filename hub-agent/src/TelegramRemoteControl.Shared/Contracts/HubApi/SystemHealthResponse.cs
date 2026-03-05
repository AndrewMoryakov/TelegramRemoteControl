namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class SystemHealthResponse
{
    public DateTime StartedAt { get; set; }
    public int TotalAgents { get; set; }
    public int OnlineAgents { get; set; }
    public int CommandsToday { get; set; }
    public long DbSizeBytes { get; set; }
}
