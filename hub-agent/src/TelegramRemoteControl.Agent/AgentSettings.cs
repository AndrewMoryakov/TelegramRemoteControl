namespace TelegramRemoteControl.Agent;

public class AgentSettings
{
    public string HubUrl { get; set; } = "http://localhost:5000";
    public string HubApiKey { get; set; } = string.Empty;
    public string AgentToken { get; set; } = string.Empty;
    public string PairingCode { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public long FileMaxDownloadBytes { get; set; } = 45L * 1024 * 1024;
    public int FilePreviewMaxChars { get; set; } = 4000;
    public int FileBinaryProbeBytes { get; set; } = 8000;
    public string FileRootPath { get; set; } = string.Empty;
}
