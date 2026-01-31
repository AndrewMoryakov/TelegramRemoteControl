using System.Text;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class PowerShellExecutor : ICommandExecutor
{
    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Arguments))
        {
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = "Укажите команду"
            };
        }

        var result = await ShellHelper.RunAsync(
            "powershell.exe",
            $"-NoProfile -Command {command.Arguments}",
            ct,
            Encoding.UTF8);

        return new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Text,
            Success = true,
            Text = result
        };
    }
}
