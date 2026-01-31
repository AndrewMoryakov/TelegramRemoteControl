namespace TelegramRemoteControl.BotService.Models;

public class FileListPayload
{
    public string? Path { get; set; }
    public List<FileItem> Items { get; set; } = new();
}
