namespace TelegramRemoteControl.Hub.Data;

public class AgentRegistration
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentToken { get; set; } = string.Empty;
    public long OwnerUserId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string? FriendlyName { get; set; }
    public DateTime RegisteredAt { get; set; }
}
