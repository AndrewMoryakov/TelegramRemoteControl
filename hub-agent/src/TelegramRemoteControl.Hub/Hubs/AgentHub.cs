using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
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
    private readonly PairingAttemptTracker _pairingAttempts;
    private readonly ILogger<AgentHub> _logger;
    private readonly string _hubApiKey;
    private readonly int _agentTokenTtlDays;
    private readonly int _pairingMaxFailuresPerMinute;

    public AgentHub(AgentManager agentManager, PendingCommandStore pendingCommands, HubDbContext db,
        PairingAttemptTracker pairingAttempts,
        IOptions<HubSettings> hubSettings, ILogger<AgentHub> logger)
    {
        _agentManager = agentManager;
        _pendingCommands = pendingCommands;
        _db = db;
        _pairingAttempts = pairingAttempts;
        _logger = logger;
        _hubApiKey = hubSettings.Value.ApiKey;
        _agentTokenTtlDays = hubSettings.Value.AgentTokenTtlDays;
        _pairingMaxFailuresPerMinute = hubSettings.Value.PairingMaxFailuresPerMinute;
    }

    public override Task OnConnectedAsync()
    {
        if (!string.IsNullOrWhiteSpace(_hubApiKey))
        {
            var http = Context.GetHttpContext();
            var provided = http?.Request.Headers["X-Hub-Key"].ToString() ?? string.Empty;
            if (provided != _hubApiKey)
            {
                _logger.LogWarning("Unauthorized SignalR connection attempt from {IP}",
                    http?.Connection.RemoteIpAddress);
                Context.Abort();
                return Task.CompletedTask;
            }
        }
        return base.OnConnectedAsync();
    }

    public async Task RegisterAgent(string credential, AgentInfo info)
    {
        var remoteKey = GetRemoteKey();

        if (_pairingAttempts.IsBlocked(remoteKey, _pairingMaxFailuresPerMinute))
        {
            _logger.LogWarning("RegisterAgent rate-limited for {Remote}", remoteKey);
            Context.Abort();
            return;
        }

        // 1. Try as AgentToken
        var registration = await _db.GetAgentByToken(credential);
        if (registration != null)
        {
            // TTL check
            if (_agentTokenTtlDays > 0 && registration.LastSeenAt.HasValue &&
                (DateTime.UtcNow - registration.LastSeenAt.Value).TotalDays > _agentTokenTtlDays)
            {
                _logger.LogWarning("Agent token expired: {AgentId}, last seen {LastSeen}",
                    registration.AgentId, registration.LastSeenAt);
                _pairingAttempts.RegisterFailure(remoteKey);
                Context.Abort();
                return;
            }

            _pairingAttempts.Clear(remoteKey);
            _agentManager.SetConnected(registration.AgentId, Context.ConnectionId, info, registration.OwnerUserId);
            _ = _db.UpdateAgentLastSeenAsync(registration.AgentId);
            _logger.LogInformation("Agent authenticated by token: {AgentId} ({MachineName})",
                registration.AgentId, info.MachineName);
            return;
        }

        // 2. Try as PairingCode
        var pairing = await _db.GetPairingRequestByCode(credential);
        if (pairing != null)
        {
            if (pairing.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Expired pairing code from {Remote}", remoteKey);
                await _db.DeletePairingRequestByCode(credential);
                _pairingAttempts.RegisterFailure(remoteKey);
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
            await _db.DeletePairingRequestByCode(credential);
            _pairingAttempts.Clear(remoteKey);

            _agentManager.SetConnected(agentId, Context.ConnectionId, info, pairing.UserId);

            // Send token back to agent
            await Clients.Caller.ReceiveToken(agentToken);

            _logger.LogInformation("Agent paired: {AgentId} ({MachineName}), Owner={UserId}",
                agentId, info.MachineName, pairing.UserId);
            return;
        }

        // 3. Nothing found — reject
        _pairingAttempts.RegisterFailure(remoteKey);
        _logger.LogWarning("Invalid credential from {Remote}, disconnecting", remoteKey);
        Context.Abort();
    }

    private string GetRemoteKey()
    {
        var http = Context.GetHttpContext();
        var ip = http?.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrEmpty(ip) ? Context.ConnectionId : ip;
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
        if (agentId == null)
        {
            // Unknown connection — happens after Hub restart when the agent's SignalR
            // connection survived (no TCP FIN). Abort the connection to force the client
            // into Reconnected/Closed handler, which will call RegisterAgent again.
            _logger.LogWarning(
                "Heartbeat from unknown connection {ConnId}, aborting to force re-register",
                Context.ConnectionId);
            Context.Abort();
            return Task.CompletedTask;
        }

        _agentManager.UpdateHeartbeat(agentId, info);
        _ = _db.UpdateAgentLastSeenAsync(agentId);
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
