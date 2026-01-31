using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution;

public interface ICommandExecutor
{
    Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default);
}
