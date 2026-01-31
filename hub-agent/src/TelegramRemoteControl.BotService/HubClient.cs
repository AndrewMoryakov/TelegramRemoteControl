using System.Net.Http.Json;
using TelegramRemoteControl.Shared.Contracts.HubApi;

namespace TelegramRemoteControl.BotService;

public class HubClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HubClient> _logger;

    public HubClient(HttpClient http, ILogger<HubClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ExecuteCommandResponse> ExecuteCommand(ExecuteCommandRequest request)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/commands/execute", request);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<ExecuteCommandResponse>()
                   ?? new ExecuteCommandResponse { Success = false, ErrorMessage = "Пустой ответ от Hub" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command on Hub");
            return new ExecuteCommandResponse
            {
                Success = false,
                ErrorMessage = $"Hub недоступен: {ex.Message}"
            };
        }
    }

    public async Task<UserSeenResponse?> ReportUserSeen(UserSeenRequest request)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/users/seen", request);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<UserSeenResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to report user seen");
            return null;
        }
    }

    public async Task SetUserNotify(UserNotifyRequest request)
    {
        var resp = await _http.PostAsJsonAsync("/api/users/notify", request);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<bool> GetUserNotifyStatus(long userId)
    {
        var resp = await _http.GetFromJsonAsync<UserNotifyStatusResponse>($"/api/users/notify/status?userId={userId}");
        return resp?.Enabled ?? false;
    }

    public async Task<List<long>> GetNotifiedUsers()
    {
        var resp = await _http.GetFromJsonAsync<UserNotifyListResponse>("/api/users/notify");
        return resp?.UserIds ?? new List<long>();
    }

    public async Task SetUserAuthorized(UserAuthorizeRequest request)
    {
        var resp = await _http.PostAsJsonAsync("/api/users/authorize", request);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<PairResponse> GeneratePairCode(long userId)
    {
        var resp = await _http.PostAsJsonAsync("/api/pair/generate", new PairRequest { UserId = userId });
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PairResponse>()
               ?? throw new Exception("Пустой ответ от Hub");
    }

    public async Task<DeviceListResponse> GetDevices(long userId)
    {
        var resp = await _http.GetFromJsonAsync<DeviceListResponse>($"/api/devices?userId={userId}");
        return resp ?? new DeviceListResponse();
    }

    public async Task SelectDevice(long userId, string agentId)
    {
        var resp = await _http.PostAsJsonAsync("/api/devices/select", new SelectDeviceRequest { UserId = userId, AgentId = agentId });
        resp.EnsureSuccessStatusCode();
    }

    public async Task<DeviceDto?> GetSelectedDevice(long userId)
    {
        var resp = await _http.GetAsync($"/api/devices/selected?userId={userId}");
        if (resp.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;

        if (resp.Content.Headers.ContentLength == 0)
            return null;

        return await resp.Content.ReadFromJsonAsync<DeviceDto?>();
    }
}
