namespace TelegramRemoteControl.Agent;

public class AiSettings
{
    public string CliPath { get; set; } = "claude";
    public int MaxTurns { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 300;
}
