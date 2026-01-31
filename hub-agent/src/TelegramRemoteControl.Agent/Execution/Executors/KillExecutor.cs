using System.Diagnostics;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class KillExecutor : ICommandExecutor
{
    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var pidText = command.Parameters != null && command.Parameters.TryGetValue("pid", out var value)
            ? value
            : command.Arguments;

        if (string.IsNullOrWhiteSpace(pidText) || !int.TryParse(pidText, out var pid))
        {
            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = "Некорректный PID"
            });
        }

        try
        {
            var process = Process.GetProcessById(pid);
            process.Kill();

            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = $"✅ Процесс {pid} завершён"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = $"Не удалось завершить процесс {pid}: {ex.Message}"
            });
        }
    }
}
