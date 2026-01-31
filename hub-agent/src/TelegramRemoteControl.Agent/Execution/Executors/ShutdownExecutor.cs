using System.Diagnostics;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class ShutdownExecutor : ICommandExecutor
{
    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        try
        {
            Process.Start(new ProcessStartInfo("shutdown", "/s /t 10")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });

            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = "🔴 Выключение через 10 секунд...\n\nОтмена: /cmd shutdown /a"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = $"Ошибка выключения: {ex.Message}"
            });
        }
    }
}
