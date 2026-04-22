using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TelegramRemoteControl.Hub.Data;
using TelegramRemoteControl.Hub.Hubs;
using TelegramRemoteControl.Hub.Services;
using TelegramRemoteControl.Shared.Contracts;
using TelegramRemoteControl.Shared.Contracts.HubApi;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Hub.Controllers;

[ApiController]
[Route("api/commands")]
public class BroadcastController : ControllerBase
{
    private readonly HubDbContext _db;
    private readonly AgentManager _agentManager;
    private readonly PendingCommandStore _pendingCommands;
    private readonly IHubContext<AgentHub, IAgentHubClient> _hubContext;
    private readonly HubSettings _settings;
    private readonly ILogger<BroadcastController> _logger;

    public BroadcastController(
        HubDbContext db,
        AgentManager agentManager,
        PendingCommandStore pendingCommands,
        IHubContext<AgentHub, IAgentHubClient> hubContext,
        IOptions<HubSettings> settings,
        ILogger<BroadcastController> logger)
    {
        _db = db;
        _agentManager = agentManager;
        _pendingCommands = pendingCommands;
        _hubContext = hubContext;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpPost("broadcast")]
    public async Task<ActionResult<List<BroadcastResultDto>>> Broadcast([FromBody] BroadcastRequest request)
    {
        var agents = await _db.GetAgentsByUser(request.UserId);
        var onlineAgents = agents
            .Select(a => (reg: a, connected: _agentManager.GetAgent(a.AgentId)))
            .Where(x => x.connected?.IsOnline == true)
            .ToList();

        if (onlineAgents.Count == 0)
        {
            return Ok(new List<BroadcastResultDto>());
        }

        var timeout = TimeSpan.FromSeconds(_settings.CommandTimeoutSeconds);

        var tasks = onlineAgents.Select(async x =>
        {
            var command = new AgentCommand
            {
                RequestId = Guid.NewGuid().ToString(),
                Type = request.CommandType,
                Arguments = request.Arguments
            };

            _logger.LogInformation("Broadcast {CommandType} to {AgentId}", command.Type, x.reg.AgentId);

            try
            {
                var responseTask = _pendingCommands.WaitForResponse(command.RequestId, timeout);
                await _hubContext.Clients.Client(x.connected!.ConnectionId).ExecuteCommand(command);
                var response = await responseTask;

                return new BroadcastResultDto
                {
                    AgentId = x.reg.AgentId,
                    MachineName = x.reg.MachineName,
                    FriendlyName = x.reg.FriendlyName,
                    Success = response.Success,
                    Text = response.Text,
                    ErrorMessage = response.ErrorMessage,
                    Data = response.Data
                };
            }
            catch (Exception ex)
            {
                // BL-10: short-circuit the TCS so the slot in PendingCommandStore
                // doesn't sit until CommandTimeoutSeconds expires.
                _pendingCommands.Complete(command.RequestId, new AgentResponse
                {
                    RequestId = command.RequestId,
                    Type = ResponseType.Error,
                    Success = false,
                    ErrorMessage = ex.Message
                });
                return new BroadcastResultDto
                {
                    AgentId = x.reg.AgentId,
                    MachineName = x.reg.MachineName,
                    FriendlyName = x.reg.FriendlyName,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        });

        var results = await Task.WhenAll(tasks);
        return Ok(results.ToList());
    }
}
