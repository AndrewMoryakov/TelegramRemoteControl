using Microsoft.AspNetCore.SignalR;
using TelegramRemoteControl.Hub.Data;
using TelegramRemoteControl.Hub.Services;
using TelegramRemoteControl.Shared.Contracts;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Hub.Hubs;

public class AgentHub : Hub<IAgentHubClient>, IAgentHubServer
{
    private readonly AgentManager _agentManager;
    private readonly PendingCommandStore _pendingCommands;
    private readonly HubDbContext _db;
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(AgentManager agentManager, PendingCommandStore pendingCommands, HubDbContext db, ILogger<AgentHub> logger)
    {
        _agentManager = agentManager;
        _pendingCommands = pendingCommands;
        _db = db;
        _logger = logger;
    }

    public async Task RegisterAgent(string credential, AgentInfo info)
    {
        // 1. Try as AgentToken
        var registration = await _db.GetAgentByToken(credential);
        if (registration != null)
        {
            _agentManager.SetConnected(registration.AgentId, Context.ConnectionId, info, registration.OwnerUserId);
            _logger.LogInformation("Agent authenticated by token: {AgentId} ({MachineName})",
                registration.AgentId, info.MachineName);
            return;
        }

        // 2. Try as PairingCode
        var pairing = await _db.GetPairingRequest(credential);
        if (pairing != null)
        {
            if (pairing.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Expired pairing code: {Code}", credential);
                await _db.DeletePairingRequest(credential);
                Context.Abort();
                return;
            }

            var agentId = Guid.NewGuid().ToString();
            var agentToken = Guid.NewGuid().ToString();

            var newAgent = new AgentRegistration
            {
                AgentId = agentId,
                AgentToken = agentToken,
                OwnerUserId = pairing.UserId,
                MachineName = info.MachineName,
                FriendlyName = info.FriendlyName,
                RegisteredAt = DateTime.UtcNow
            };

            await _db.AddAgent(newAgent);
            await _db.DeletePairingRequest(credential);

            _agentManager.SetConnected(agentId, Context.ConnectionId, info, pairing.UserId);

            // Send token back to agent
            await Clients.Caller.ReceiveToken(agentToken);

            _logger.LogInformation("Agent paired: {AgentId} ({MachineName}), Owner={UserId}",
                agentId, info.MachineName, pairing.UserId);
            return;
        }

        // 3. Nothing found — reject
        _logger.LogWarning("Invalid credential, disconnecting: {Credential}", credential);
        Context.Abort();
    }

    public Task SendResponse(AgentResponse response)
    {
        _logger.LogDebug("Response received: RequestId={RequestId}, Type={Type}, Success={Success}",
            response.RequestId, response.Type, response.Success);
        _pendingCommands.Complete(response.RequestId, response);
        return Task.CompletedTask;
    }

    public Task Heartbeat(AgentInfo info)
    {
        var agentId = _agentManager.GetAgentIdByConnection(Context.ConnectionId);
        if (agentId != null)
        {
            _agentManager.UpdateHeartbeat(agentId, info);
        }
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var agentId = _agentManager.GetAgentIdByConnection(Context.ConnectionId);
        _agentManager.SetDisconnected(Context.ConnectionId);
        _logger.LogInformation("Agent disconnected: {AgentId}, ConnectionId={ConnectionId}",
            agentId ?? "unknown", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
