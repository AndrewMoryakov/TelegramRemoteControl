namespace TelegramRemoteControl.Hub.Data;

public class PairingRequest
{
    public string Code { get; set; } = string.Empty;
    public long UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
}
