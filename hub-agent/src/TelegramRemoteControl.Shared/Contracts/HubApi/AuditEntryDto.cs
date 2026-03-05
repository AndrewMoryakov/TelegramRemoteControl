namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class AuditEntryDto
{
    public DateTime Timestamp { get; set; }
    public long UserId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string CommandType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public long DurationMs { get; set; }
}
