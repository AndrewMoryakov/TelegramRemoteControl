namespace TelegramRemoteControl.Agent;

public class AiSettings
{
    /// <summary>"claude" or "openrouter"</summary>
    public string Provider { get; set; } = "claude";

    // Claude CLI settings
    public string CliPath { get; set; } = "claude";
    public string Model { get; set; } = string.Empty;
    public int MaxTurns { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 300;

    // OpenRouter settings
    public string OpenRouterApiKey { get; set; } = string.Empty;
}
