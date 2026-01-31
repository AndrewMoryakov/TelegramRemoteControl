using System.Management;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class UptimeExecutor : ICommandExecutor
{
    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var bootTime = DateTime.Now - uptime;

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                var lastBootStr = obj["LastBootUpTime"]?.ToString();
                if (!string.IsNullOrEmpty(lastBootStr))
                {
                    bootTime = ManagementDateTimeConverter.ToDateTime(lastBootStr);
                }
            }
        }
        catch { }

        var text = $"⏱ Время работы системы\n\n" +
                   $"🟢 Uptime: {uptime.Days} дн. {uptime.Hours} ч. {uptime.Minutes} мин.\n" +
                   $"🔄 Запуск: {bootTime:dd.MM.yyyy HH:mm:ss}\n" +
                   $"📅 Сейчас: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

        return Task.FromResult(new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Text,
            Success = true,
            Text = text
        });
    }
}
