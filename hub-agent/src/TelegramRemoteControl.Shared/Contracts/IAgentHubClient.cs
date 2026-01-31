using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Shared.Contracts;

/// <summary>
/// Methods the Hub can call on the Agent (Hub -> Agent).
/// </summary>
public interface IAgentHubClient
{
    Task ExecuteCommand(AgentCommand command);
    Task<AgentInfo> Ping();
    Task ReceiveToken(string agentToken);
}
