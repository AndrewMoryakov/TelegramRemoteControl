namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class PairResponse
{
    public string Code { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
