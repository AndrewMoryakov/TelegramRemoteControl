using Microsoft.Extensions.Options;

namespace TelegramRemoteControl.Hub.Services;

public class AgentLivenessMonitor : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private readonly AgentManager _agentManager;
    private readonly HubSettings _settings;
    private readonly ILogger<AgentLivenessMonitor> _logger;

    public AgentLivenessMonitor(
        AgentManager agentManager,
        IOptions<HubSettings> settings,
        ILogger<AgentLivenessMonitor> logger)
    {
        _agentManager = agentManager;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var threshold = TimeSpan.FromSeconds(Math.Max(30, _settings.AgentTimeoutSeconds));
                var now = DateTime.UtcNow;
                foreach (var agent in _agentManager.GetAllAgents())
                {
                    if (!agent.IsOnline) continue;
                    if (now - agent.LastHeartbeat > threshold)
                    {
                        _logger.LogWarning(
                            "Agent {AgentId} timed out (last heartbeat {LastHeartbeat:o}), marking offline",
                            agent.AgentId, agent.LastHeartbeat);
                        _agentManager.SetDisconnected(agent.ConnectionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AgentLivenessMonitor iteration failed");
            }

            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
