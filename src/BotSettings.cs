namespace TelegramRemoteControl;

public class BotSettings
{
    public string Token { get; set; } = string.Empty;
    public long[] AuthorizedUsers { get; set; } = Array.Empty<long>();
}
