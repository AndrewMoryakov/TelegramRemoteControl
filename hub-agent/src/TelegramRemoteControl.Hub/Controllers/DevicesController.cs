using Microsoft.AspNetCore.Mvc;
using TelegramRemoteControl.Hub.Data;
using TelegramRemoteControl.Hub.Services;
using TelegramRemoteControl.Shared.Contracts.HubApi;

namespace TelegramRemoteControl.Hub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly HubDbContext _db;
    private readonly AgentManager _agentManager;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(HubDbContext db, AgentManager agentManager, ILogger<DevicesController> logger)
    {
        _db = db;
        _agentManager = agentManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<DeviceListResponse>> GetDevices([FromQuery] long userId)
    {
        var registrations = await _db.GetAgentsByUser(userId);

        var devices = registrations.Select(r =>
        {
            var connected = _agentManager.GetAgent(r.AgentId);
            return new DeviceDto
            {
                AgentId = r.AgentId,
                MachineName = r.MachineName,
                FriendlyName = r.FriendlyName,
                IsOnline = connected?.IsOnline ?? false,
                LastSeen = connected?.LastHeartbeat
            };
        }).ToList();

        return Ok(new DeviceListResponse { Devices = devices });
    }

    [HttpPost("select")]
    public async Task<IActionResult> SelectDevice([FromBody] SelectDeviceRequest request)
    {
        var agent = await _db.GetAgentById(request.AgentId);
        if (agent == null)
            return NotFound("Устройство не найдено");

        if (agent.OwnerUserId != request.UserId)
            return Forbid();

        await _db.SetSelectedAgent(request.UserId, request.AgentId);
        _logger.LogInformation("User {UserId} selected agent {AgentId}", request.UserId, request.AgentId);
        return Ok();
    }

    [HttpGet("selected")]
    public async Task<ActionResult<DeviceDto?>> GetSelectedDevice([FromQuery] long userId)
    {
        var agentId = await _db.GetSelectedAgent(userId);
        if (agentId == null)
            return Ok(null as DeviceDto);

        var reg = await _db.GetAgentById(agentId);
        if (reg == null)
            return Ok(null as DeviceDto);

        var connected = _agentManager.GetAgent(agentId);
        return Ok(new DeviceDto
        {
            AgentId = reg.AgentId,
            MachineName = reg.MachineName,
            FriendlyName = reg.FriendlyName,
            IsOnline = connected?.IsOnline ?? false,
            LastSeen = connected?.LastHeartbeat
        });
    }
}
