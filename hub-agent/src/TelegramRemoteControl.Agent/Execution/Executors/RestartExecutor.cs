using System.Diagnostics;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class RestartExecutor : ICommandExecutor
{
    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        try
        {
            Process.Start(new ProcessStartInfo("shutdown", "/r /t 10")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });

            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = "🔄 Перезагрузка через 10 секунд...\n\nОтмена: /cmd shutdown /a"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = $"Ошибка перезагрузки: {ex.Message}"
            });
        }
    }
}
