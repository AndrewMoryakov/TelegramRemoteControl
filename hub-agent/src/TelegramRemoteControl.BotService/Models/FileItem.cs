namespace TelegramRemoteControl.BotService.Models;

public class FileItem
{
    public string Name { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime? Modified { get; set; }
    public long? Free { get; set; }
    public long? Total { get; set; }
}
