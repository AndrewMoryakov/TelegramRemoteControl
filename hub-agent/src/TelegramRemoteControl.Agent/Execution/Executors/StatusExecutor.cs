using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class StatusExecutor : ICommandExecutor
{
    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var text = $"🖥 Компьютер: {Environment.MachineName}\n" +
                   $"👤 Пользователь: {Environment.UserName}\n" +
                   $"💻 ОС: {Environment.OSVersion}\n" +
                   $"⏱ Uptime: {uptime.Days}д {uptime.Hours}ч {uptime.Minutes}м\n" +
                   $"🔧 CPU: {Environment.ProcessorCount} ядер";

        return Task.FromResult(new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Text,
            Success = true,
            Text = text
        });
    }
}
