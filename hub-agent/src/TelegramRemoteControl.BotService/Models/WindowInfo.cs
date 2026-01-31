namespace TelegramRemoteControl.BotService.Models;

public class WindowInfo
{
    public long Hwnd { get; set; }
    public int Pid { get; set; }
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}
