using System.Runtime.InteropServices;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class SleepExecutor : ICommandExecutor
{
    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        try
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                SetSuspendState(false, false, false);
            }, ct);

            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = "😴 Переход в режим сна..."
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = $"Ошибка сна: {ex.Message}"
            });
        }
    }
}
