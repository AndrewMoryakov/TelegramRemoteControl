using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class AgentConfigExecutor : ICommandExecutor
{
    private readonly AiSettings _aiSettings;
    private readonly string _settingsPath;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public AgentConfigExecutor(AiSettings aiSettings, string settingsPath)
    {
        _aiSettings = aiSettings;
        _settingsPath = settingsPath;
    }

    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var parts = (command.Arguments ?? string.Empty).Split(':', 3);
        var action = parts[0];

        return action switch
        {
            "get"        => GetSettings(command),
            "get-models" => await GetModelsAsync(command, ct),
            "set" when parts.Length >= 3 => Set(command, parts[1], parts[2]),
            _ => Error(command, "Неизвестная команда. Используйте: get | get-models | set:key:value")
        };
    }

    private AgentResponse GetSettings(AgentCommand command)
    {
        var payload = JsonSerializer.Serialize(new
        {
            provider         = _aiSettings.Provider,
            model            = _aiSettings.Model,
            maxTurns         = _aiSettings.MaxTurns,
            timeoutSeconds   = _aiSettings.TimeoutSeconds,
            cliPath          = _aiSettings.CliPath,
            hasOpenRouterKey = !string.IsNullOrWhiteSpace(_aiSettings.OpenRouterApiKey)
        });

        return new AgentResponse
        {
            RequestId   = command.RequestId,
            Type        = ResponseType.Text,
            Success     = true,
            JsonPayload = payload,
            Text        = "ok"
        };
    }

    private async Task<AgentResponse> GetModelsAsync(AgentCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_aiSettings.OpenRouterApiKey))
            return Error(command, "OpenRouter API ключ не настроен. Введите его через настройки.");

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/models");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _aiSettings.OpenRouterApiKey);
            req.Headers.Add("HTTP-Referer", "https://github.com/TelegramRemoteControl");
            req.Headers.Add("X-Title", "TelepilotHub");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var resp = await _http.SendAsync(req, cts.Token);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return Error(command, "Неверный OpenRouter API ключ (401)");

            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(cts.Token);
            return new AgentResponse
            {
                RequestId   = command.RequestId,
                Type        = ResponseType.Text,
                Success     = true,
                JsonPayload = json,
                Text        = "ok"
            };
        }
        catch (OperationCanceledException)
        {
            return Error(command, "Таймаут запроса к OpenRouter");
        }
        catch (Exception ex)
        {
            return Error(command, $"Ошибка при обращении к OpenRouter: {ex.Message}");
        }
    }

    private AgentResponse Set(AgentCommand command, string key, string value)
    {
        var error = ApplySetting(key, value);
        if (error != null)
            return Error(command, error);

        PersistSettings();

        return new AgentResponse
        {
            RequestId = command.RequestId,
            Type      = ResponseType.Text,
            Success   = true,
            Text      = $"✅ {key} обновлён"
        };
    }

    private string? ApplySetting(string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "provider":
                if (value != "claude" && value != "openrouter")
                    return "Провайдер должен быть 'claude' или 'openrouter'";
                _aiSettings.Provider = value;
                return null;

            case "model":
                _aiSettings.Model = value;
                return null;

            case "maxturns":
                if (!int.TryParse(value, out var turns) || turns < 1 || turns > 50)
                    return "MaxTurns должен быть числом от 1 до 50";
                _aiSettings.MaxTurns = turns;
                return null;

            case "timeout":
                if (!int.TryParse(value, out var timeout) || timeout < 10 || timeout > 3600)
                    return "Таймаут должен быть от 10 до 3600 секунд";
                _aiSettings.TimeoutSeconds = timeout;
                return null;

            case "clipath":
                if (string.IsNullOrWhiteSpace(value))
                    return "CliPath не может быть пустым";
                _aiSettings.CliPath = value;
                return null;

            case "openrouterkey":
                _aiSettings.OpenRouterApiKey = value;
                return null;

            default:
                return $"Неизвестный параметр: {key}";
        }
    }

    private void PersistSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            var json = JsonNode.Parse(File.ReadAllText(_settingsPath, Encoding.UTF8)) as JsonObject
                       ?? new JsonObject();

            var ai = json["Ai"] as JsonObject ?? new JsonObject();
            ai["Provider"]         = _aiSettings.Provider;
            ai["Model"]            = _aiSettings.Model;
            ai["MaxTurns"]         = _aiSettings.MaxTurns;
            ai["TimeoutSeconds"]   = _aiSettings.TimeoutSeconds;
            ai["CliPath"]          = _aiSettings.CliPath;
            ai["OpenRouterApiKey"] = _aiSettings.OpenRouterApiKey;
            json["Ai"]             = ai;

            File.WriteAllText(_settingsPath,
                json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);
        }
        catch
        {
            // in-memory settings already updated — persistence is best-effort
        }
    }

    private static AgentResponse Error(AgentCommand command, string message) => new()
    {
        RequestId    = command.RequestId,
        Type         = ResponseType.Error,
        Success      = false,
        ErrorMessage = message
    };
}
