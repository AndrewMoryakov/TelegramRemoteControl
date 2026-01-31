namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class UserAuthorizeRequest
{
    public long UserId { get; init; }
    public bool Authorized { get; init; }
}
