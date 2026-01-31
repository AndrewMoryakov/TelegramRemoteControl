using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TelegramRemoteControl.Hub.Data;
using TelegramRemoteControl.Shared.Contracts.HubApi;

namespace TelegramRemoteControl.Hub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PairController : ControllerBase
{
    private readonly HubDbContext _db;
    private readonly HubSettings _settings;
    private readonly ILogger<PairController> _logger;

    public PairController(HubDbContext db, IOptions<HubSettings> settings, ILogger<PairController> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<PairResponse>> Generate([FromBody] PairRequest request)
    {
        var agentCount = await _db.GetAgentCountByUser(request.UserId);
        if (agentCount >= _settings.MaxAgentsPerUser)
        {
            return BadRequest($"Достигнут лимит устройств ({_settings.MaxAgentsPerUser})");
        }

        var code = GenerateCode(6);
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        await _db.AddPairingRequest(new PairingRequest
        {
            Code = code,
            UserId = request.UserId,
            ExpiresAt = expiresAt
        });

        _logger.LogInformation("Pairing code generated: {Code} for user {UserId}", code, request.UserId);

        return Ok(new PairResponse
        {
            Code = code,
            ExpiresAt = expiresAt
        });
    }

    private static string GenerateCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I,O,0,1
        return string.Create(length, chars, (span, c) =>
        {
            var bytes = RandomNumberGenerator.GetBytes(length);
            for (int i = 0; i < span.Length; i++)
                span[i] = c[bytes[i] % c.Length];
        });
    }
}
