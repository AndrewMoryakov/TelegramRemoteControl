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
[Route("api/[controller]")]
public class CommandsController : ControllerBase
{
    private readonly HubDbContext _db;
    private readonly AgentManager _agentManager;
    private readonly PendingCommandStore _pendingCommands;
    private readonly IHubContext<AgentHub, IAgentHubClient> _hubContext;
    private readonly HubSettings _settings;
    private readonly ILogger<CommandsController> _logger;

    public CommandsController(
        HubDbContext db,
        AgentManager agentManager,
        PendingCommandStore pendingCommands,
        IHubContext<AgentHub, IAgentHubClient> hubContext,
        IOptions<HubSettings> settings,
        ILogger<CommandsController> logger)
    {
        _db = db;
        _agentManager = agentManager;
        _pendingCommands = pendingCommands;
        _hubContext = hubContext;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpPost("execute")]
    public async Task<ActionResult<ExecuteCommandResponse>> Execute([FromBody] ExecuteCommandRequest request)
    {
        // Get selected agent for user
        var agentId = await _db.GetSelectedAgent(request.UserId);
        ConnectedAgent? agent = null;

        if (agentId == null)
        {
            return Ok(new ExecuteCommandResponse
            {
                Success = false,
                Type = ResponseType.Error,
                ErrorMessage = "Выберите ПК: /pc"
            });
        }

        agent = _agentManager.GetAgent(agentId);
        if (agent != null && agent.OwnerUserId != request.UserId)
            agent = null; // ownership check

        if (agent == null)
        {
            return Ok(new ExecuteCommandResponse
            {
                Success = false,
                Type = ResponseType.Error,
                ErrorMessage = "Выберите ПК: /pc"
            });
        }

        if (!agent.IsOnline)
        {
            return Ok(new ExecuteCommandResponse
            {
                Success = false,
                Type = ResponseType.Error,
                ErrorMessage = $"🔴 {agent.Info.FriendlyName ?? agent.Info.MachineName} не в сети"
            });
        }

        var command = new AgentCommand
        {
            RequestId = Guid.NewGuid().ToString(),
            Type = request.CommandType,
            Arguments = request.Arguments,
            Parameters = request.Parameters
        };

        _logger.LogInformation("Executing {CommandType} on {AgentId} for user {UserId}, RequestId={RequestId}",
            command.Type, agent.AgentId, request.UserId, command.RequestId);

        var timeout = TimeSpan.FromSeconds(_settings.CommandTimeoutSeconds);
        var responseTask = _pendingCommands.WaitForResponse(command.RequestId, timeout);

        await _hubContext.Clients.Client(agent.ConnectionId).ExecuteCommand(command);

        var response = await responseTask;

        return Ok(new ExecuteCommandResponse
        {
            Success = response.Success,
            Type = response.Type,
            Text = response.Text,
            ErrorMessage = response.ErrorMessage,
            Data = response.Data,
            FileName = response.FileName,
            JsonPayload = response.JsonPayload,
            Buttons = response.Buttons
        });
    }
}
