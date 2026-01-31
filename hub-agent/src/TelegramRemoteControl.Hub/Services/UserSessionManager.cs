using System.Collections.Concurrent;

namespace TelegramRemoteControl.Hub.Services;

public class UserSessionManager
{
    private readonly ConcurrentDictionary<long, string> _selectedAgent = new();

    public string? GetSelectedAgent(long userId)
    {
        _selectedAgent.TryGetValue(userId, out var agentId);
        return agentId;
    }

    public void SetSelectedAgent(long userId, string agentId)
    {
        _selectedAgent[userId] = agentId;
    }
}
