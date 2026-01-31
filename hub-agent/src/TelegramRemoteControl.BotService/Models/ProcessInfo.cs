namespace TelegramRemoteControl.BotService.Models;

public class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Ram { get; set; }
    public double Cpu { get; set; }
}
