using Microsoft.AspNetCore.Mvc;
using TelegramRemoteControl.Hub.Data;
using TelegramRemoteControl.Shared.Contracts.HubApi;

namespace TelegramRemoteControl.Hub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly HubDbContext _db;
    private readonly ILogger<UsersController> _logger;

    public UsersController(HubDbContext db, ILogger<UsersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("seen")]
    public async Task<ActionResult<UserSeenResponse>> Seen([FromBody] UserSeenRequest request)
    {
        await _db.UpsertUser(request.UserId, request.Username, request.FirstName);
        var isAuthorized = await _db.GetUserAuthorized(request.UserId);
        return Ok(new UserSeenResponse
        {
            UserId = request.UserId,
            IsAuthorized = isAuthorized
        });
    }

    [HttpPost("notify")]
    public async Task<IActionResult> SetNotify([FromBody] UserNotifyRequest request)
    {
        await _db.SetUserNotifyStatus(request.UserId, request.Enabled);
        _logger.LogInformation("User {UserId} notify status set to {Enabled}", request.UserId, request.Enabled);
        return Ok();
    }

    [HttpPost("authorize")]
    public async Task<IActionResult> SetAuthorized([FromBody] UserAuthorizeRequest request)
    {
        await _db.SetUserAuthorized(request.UserId, request.Authorized);
        _logger.LogInformation("User {UserId} authorization set to {Authorized}", request.UserId, request.Authorized);
        return Ok();
    }

    [HttpGet("notify")]
    public async Task<ActionResult<UserNotifyListResponse>> GetNotifiedUsers()
    {
        var users = await _db.GetNotifiedUsers();
        return Ok(new UserNotifyListResponse { UserIds = users });
    }

    [HttpGet("notify/status")]
    public async Task<ActionResult<UserNotifyStatusResponse>> GetNotifyStatus([FromQuery] long userId)
    {
        var enabled = await _db.GetUserNotifyStatus(userId);
        return Ok(new UserNotifyStatusResponse { UserId = userId, Enabled = enabled });
    }
}
