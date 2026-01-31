using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class CmdExecutor : ICommandExecutor
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

        var result = await ShellHelper.RunAsync("cmd.exe", $"/c {command.Arguments}", ct);

        return new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Text,
            Success = true,
            Text = result
        };
    }
}
