namespace TelegramRemoteControl.Hub;

public class HubSettings
{
    public string DatabasePath { get; set; } = "hub.db";
    public int CommandTimeoutSeconds { get; set; } = 120;
    public int MaxMessageSizeBytes { get; set; } = 52_428_800; // 50 MB
    public string ApiKey { get; set; } = string.Empty;
    public int AgentTimeoutSeconds { get; set; } = 90;
    public int MaxAgentsPerUser { get; set; } = 10;
}
