using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using TelegramRemoteControl.Agent.Execution;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent;

public class AgentService : BackgroundService
{
    private readonly AgentSettings _settings;
    private readonly CommandExecutor _executor;
    private readonly ILogger<AgentService> _logger;
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    // BL-09: gates the heartbeat loop while a pairing-triggered reconnect or an
    // auto-reconnect re-register is in flight. Prevents a heartbeat from landing
    // on the new connection before RegisterAgent has run, which would show up in
    // the Hub as "unknown connection heartbeat" and force another abort loop.
    private readonly ManualResetEventSlim _readyForHeartbeat = new(true);
    private HubConnection? _connection;

    public AgentService(
        IOptions<AgentSettings> settings,
        CommandExecutor executor,
        ILogger<AgentService> logger,
        IHostEnvironment environment)
    {
        _settings = settings.Value;
        _executor = executor;
        _logger = logger;
        _settingsPath = Path.Combine(environment.ContentRootPath, "appsettings.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var credential = GetCredential();

        if (string.IsNullOrEmpty(credential))
        {
            _logger.LogError("AgentToken or PairingCode must be set in appsettings.json");
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl($"{_settings.HubUrl}/agent-hub", options =>
            {
                if (!string.IsNullOrWhiteSpace(_settings.HubApiKey))
                    options.Headers.Add("X-Hub-Key", _settings.HubApiKey);
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .AddMessagePackProtocol()
            .Build();

        _connection.On<AgentCommand>("ExecuteCommand", async command =>
        {
            var response = await _executor.ExecuteAsync(command, stoppingToken);
            await _connection.InvokeAsync("SendResponse", response, stoppingToken);
        });

        _connection.On<string>("ReceiveToken", async agentToken =>
        {
            await HandleReceiveTokenAsync(agentToken, stoppingToken);
        });

        _connection.Reconnected += async _ =>
        {
            _logger.LogInformation("Reconnected to Hub. Re-registering...");
            _readyForHeartbeat.Reset();
            try
            {
                await RegisterAgent(GetCredential(), stoppingToken);
            }
            catch (Exception ex)
            {
                // If re-register fails after an auto-reconnect, the next Heartbeat will
                // land on an unknown connection in the Hub → Hub aborts → we land here
                // again on the next Reconnected. Logging is enough.
                _logger.LogError(ex, "Re-register after reconnect failed");
            }
            finally
            {
                _readyForHeartbeat.Set();
            }
        };

        _connection.Closed += ex =>
        {
            _logger.LogWarning(ex, "Connection to Hub closed");
            return Task.CompletedTask;
        };

        // Connect + Register with retry. Both operations are in the same loop so that
        // a failed RegisterAgent tears the connection down and we start fresh, instead
        // of sitting in a connected-but-unregistered state (zombie heartbeat).
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _connection.StartAsync(stoppingToken);
                _logger.LogInformation("Connected to Hub at {HubUrl}", _settings.HubUrl);
                await RegisterAgent(credential, stoppingToken);
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connect+Register failed. Retrying in 5s...");
                try { await _connection.StopAsync(stoppingToken); } catch { /* best effort */ }
                await Task.Delay(5000, stoppingToken);
            }
        }

        // Heartbeat loop
        var agentInfo = BuildAgentInfo();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.HeartbeatIntervalSeconds), stoppingToken);
                _readyForHeartbeat.Wait(stoppingToken);
                if (_connection.State == HubConnectionState.Connected)
                {
                    await _connection.InvokeAsync("Heartbeat", agentInfo, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat failed");
            }
        }
    }

    private string GetCredential()
    {
        if (!string.IsNullOrWhiteSpace(_settings.AgentToken))
            return _settings.AgentToken;
        if (!string.IsNullOrWhiteSpace(_settings.PairingCode))
            return _settings.PairingCode;
        return string.Empty;
    }

    private async Task HandleReceiveTokenAsync(string agentToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(agentToken))
        {
            _logger.LogWarning("Received empty agent token");
            return;
        }

        var updated = false;
        if (!string.Equals(_settings.AgentToken, agentToken, StringComparison.Ordinal))
        {
            _settings.AgentToken = agentToken;
            updated = true;
        }
        if (!string.IsNullOrWhiteSpace(_settings.PairingCode))
        {
            _settings.PairingCode = string.Empty;
            updated = true;
        }

        if (!updated)
            return;

        if (!TryPersistToken(agentToken))
            _logger.LogWarning("Failed to persist AgentToken to {Path}", _settingsPath);

        await ReconnectWithTokenAsync(agentToken, ct);
    }

    private async Task ReconnectWithTokenAsync(string agentToken, CancellationToken ct)
    {
        if (_connection == null)
            return;

        await _reconnectLock.WaitAsync(ct);
        _readyForHeartbeat.Reset();
        try
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                _logger.LogInformation("Reconnecting with paired agent token...");
                await _connection.StopAsync(ct);
            }

            if (ct.IsCancellationRequested)
                return;

            await _connection.StartAsync(ct);
            await RegisterAgent(agentToken, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // ignore shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconnect with agent token");
        }
        finally
        {
            _readyForHeartbeat.Set();
            _reconnectLock.Release();
        }
    }

    private bool TryPersistToken(string agentToken)
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                _logger.LogWarning("appsettings.json not found at {Path}", _settingsPath);
                return false;
            }

            var json = JsonNode.Parse(File.ReadAllText(_settingsPath, Encoding.UTF8)) as JsonObject ?? new JsonObject();
            var agentSection = json["Agent"] as JsonObject ?? new JsonObject();
            agentSection["AgentToken"] = agentToken;
            agentSection["PairingCode"] = string.Empty;
            json["Agent"] = agentSection;

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_settingsPath, json.ToJsonString(options), Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update appsettings.json with agent token");
            return false;
        }
    }

    private async Task RegisterAgent(string credential, CancellationToken ct)
    {
        var info = BuildAgentInfo();
        await _connection!.InvokeAsync("RegisterAgent", credential, info, ct);
        _logger.LogInformation("Registered as {MachineName} ({AgentId})", info.MachineName, info.AgentId);
    }

    private AgentInfo BuildAgentInfo()
    {
        return new AgentInfo
        {
            AgentId = _settings.AgentToken.Length > 0 ? _settings.AgentToken : Environment.MachineName,
            MachineName = Environment.MachineName,
            FriendlyName = _settings.FriendlyName,
            OsVersion = Environment.OSVersion.ToString(),
            UserName = Environment.UserName
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection != null)
        {
            await _connection.StopAsync(cancellationToken);
            await _connection.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
