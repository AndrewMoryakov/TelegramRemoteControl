using System.Security.Cryptography;
using System.Text;

namespace TelegramRemoteControl.Hub.Data;

public class PairingRequest
{
    public string CodeHash { get; set; } = string.Empty;
    public long UserId { get; set; }
    public DateTime ExpiresAt { get; set; }

    public static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
}
