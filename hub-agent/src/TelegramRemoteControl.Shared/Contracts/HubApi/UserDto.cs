namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class UserDto
{
    public long UserId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsAuthorized { get; set; }
}
