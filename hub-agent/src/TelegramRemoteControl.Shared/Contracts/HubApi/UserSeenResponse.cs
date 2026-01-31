namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class UserSeenResponse
{
    public long UserId { get; init; }
    public bool IsAuthorized { get; init; }
}
