namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class UserNotifyStatusResponse
{
    public long UserId { get; init; }
    public bool Enabled { get; init; }
}
