namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class UserNotifyRequest
{
    public long UserId { get; init; }
    public bool Enabled { get; init; }
}
