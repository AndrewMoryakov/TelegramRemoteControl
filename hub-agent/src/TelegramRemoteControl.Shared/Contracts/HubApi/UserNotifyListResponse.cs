namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class UserNotifyListResponse
{
    public List<long> UserIds { get; init; } = new();
}
