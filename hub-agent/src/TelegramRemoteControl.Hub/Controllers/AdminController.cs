using Microsoft.AspNetCore.Mvc;
using TelegramRemoteControl.Hub.Data;
using TelegramRemoteControl.Hub.Services;
using TelegramRemoteControl.Shared.Contracts.HubApi;

namespace TelegramRemoteControl.Hub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private static readonly DateTime StartedAt = DateTime.UtcNow;

    private readonly HubDbContext _db;
    private readonly AgentManager _agentManager;

    public AdminController(HubDbContext db, AgentManager agentManager)
    {
        _db = db;
        _agentManager = agentManager;
    }

    [HttpGet("health")]
    public async Task<ActionResult<SystemHealthResponse>> GetHealth()
    {
        var (totalAgents, commandsToday) = await _db.GetStatsAsync();
        var allConnected = _agentManager.GetAllAgents();
        var dbSize = System.IO.File.Exists(_db.DbPath) ? new System.IO.FileInfo(_db.DbPath).Length : 0;

        return Ok(new SystemHealthResponse
        {
            StartedAt = StartedAt,
            TotalAgents = totalAgents,
            OnlineAgents = allConnected.Count(a => a.IsOnline),
            CommandsToday = commandsToday,
            DbSizeBytes = dbSize
        });
    }

    [HttpGet("recent-commands")]
    public async Task<ActionResult<List<AuditEntryDto>>> GetRecentCommands([FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);
        var rows = await _db.GetRecentCommandsAsync(limit);

        var result = rows.Select(r => new AuditEntryDto
        {
            Timestamp = r.Timestamp,
            AgentId = r.AgentId,
            CommandType = r.CommandType,
            Success = r.Success,
            DurationMs = r.DurationMs
        }).ToList();

        return Ok(result);
    }
}
