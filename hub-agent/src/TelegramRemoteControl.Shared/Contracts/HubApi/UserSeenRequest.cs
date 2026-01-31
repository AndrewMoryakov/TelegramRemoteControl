namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class UserSeenRequest
{
    public long UserId { get; init; }
    public string? Username { get; init; }
    public string? FirstName { get; init; }
}
