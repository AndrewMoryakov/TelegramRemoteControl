using System.Collections.Concurrent;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Hub.Services;

public class ConnectedAgent
{
    public string AgentId { get; init; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public AgentInfo Info { get; set; } = new();
    public bool IsOnline { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public long? OwnerUserId { get; set; }
}

public class AgentManager
{
    private readonly ConcurrentDictionary<string, ConnectedAgent> _agentById = new();
    private readonly ConcurrentDictionary<string, string> _connectionToAgent = new();

    public void SetConnected(string agentId, string connectionId, AgentInfo info, long? ownerUserId = null)
    {
        var agent = _agentById.AddOrUpdate(agentId,
            _ => new ConnectedAgent
            {
                AgentId = agentId,
                ConnectionId = connectionId,
                Info = info,
                IsOnline = true,
                LastHeartbeat = DateTime.UtcNow,
                OwnerUserId = ownerUserId
            },
            (_, existing) =>
            {
                existing.ConnectionId = connectionId;
                existing.Info = info;
                existing.IsOnline = true;
                existing.LastHeartbeat = DateTime.UtcNow;
                if (ownerUserId.HasValue)
                    existing.OwnerUserId = ownerUserId;
                return existing;
            });

        _connectionToAgent[connectionId] = agentId;
    }

    public void SetDisconnected(string connectionId)
    {
        if (_connectionToAgent.TryRemove(connectionId, out var agentId))
        {
            if (_agentById.TryGetValue(agentId, out var agent))
            {
                agent.IsOnline = false;
            }
        }
    }

    public void UpdateHeartbeat(string agentId, AgentInfo info)
    {
        if (_agentById.TryGetValue(agentId, out var agent))
        {
            agent.LastHeartbeat = DateTime.UtcNow;
            agent.Info = info;
        }
    }

    public ConnectedAgent? GetAgent(string agentId)
    {
        _agentById.TryGetValue(agentId, out var agent);
        return agent;
    }

    public string? GetAgentIdByConnection(string connectionId)
    {
        _connectionToAgent.TryGetValue(connectionId, out var agentId);
        return agentId;
    }

    public ConnectedAgent? GetFirstOnlineAgent()
    {
        return _agentById.Values.FirstOrDefault(a => a.IsOnline);
    }

    public IReadOnlyList<ConnectedAgent> GetAllAgents()
    {
        return _agentById.Values.ToList();
    }

    public IReadOnlyList<ConnectedAgent> GetAgentsByOwner(long userId)
    {
        return _agentById.Values.Where(a => a.OwnerUserId == userId).ToList();
    }
}
