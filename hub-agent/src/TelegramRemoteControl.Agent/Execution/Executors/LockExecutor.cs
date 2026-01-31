using System.Runtime.InteropServices;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class LockExecutor : ICommandExecutor
{
    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        try
        {
            LockWorkStation();
            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = "🔒 Экран заблокирован"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = $"Ошибка блокировки: {ex.Message}"
            });
        }
    }
}
