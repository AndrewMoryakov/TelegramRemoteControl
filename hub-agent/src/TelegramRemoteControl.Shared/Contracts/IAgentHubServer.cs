using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Shared.Contracts;

/// <summary>
/// Methods the Agent can call on the Hub (Agent -> Hub).
/// </summary>
public interface IAgentHubServer
{
    Task RegisterAgent(string agentToken, AgentInfo info);
    Task SendResponse(AgentResponse response);
    Task Heartbeat(AgentInfo info);
}
