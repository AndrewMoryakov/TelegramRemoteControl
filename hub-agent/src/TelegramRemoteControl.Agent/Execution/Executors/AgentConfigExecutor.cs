using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class AgentConfigExecutor : ICommandExecutor
{
    private readonly AiSettings _aiSettings;
    private readonly string _settingsPath;

    public AgentConfigExecutor(AiSettings aiSettings, string settingsPath)
    {
        _aiSettings = aiSettings;
        _settingsPath = settingsPath;
    }

    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        // Arguments: "get" | "set:key:value"
        var parts = (command.Arguments ?? string.Empty).Split(':', 3);
        var action = parts[0];

        if (action == "get")
        {
            var payload = JsonSerializer.Serialize(new
            {
                model          = _aiSettings.Model,
                maxTurns       = _aiSettings.MaxTurns,
                timeoutSeconds = _aiSettings.TimeoutSeconds,
                cliPath        = _aiSettings.CliPath
            });

            return Task.FromResult(new AgentResponse
            {
                RequestId   = command.RequestId,
                Type        = ResponseType.Text,
                Success     = true,
                JsonPayload = payload,
                Text        = "ok"
            });
        }

        if (action == "set" && parts.Length >= 3)
        {
            var key   = parts[1];
            var value = parts[2];

            var error = ApplySetting(key, value);
            if (error != null)
                return Task.FromResult(new AgentResponse
                {
                    RequestId    = command.RequestId,
                    Type         = ResponseType.Error,
                    Success      = false,
                    ErrorMessage = error
                });

            PersistSettings();

            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type      = ResponseType.Text,
                Success   = true,
                Text      = $"✅ {key} = {value}"
            });
        }

        return Task.FromResult(new AgentResponse
        {
            RequestId    = command.RequestId,
            Type         = ResponseType.Error,
            Success      = false,
            ErrorMessage = "Неизвестная команда. Используйте: get | set:key:value"
        });
    }

    private string? ApplySetting(string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
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
                    return "Таймаут должен быть числом от 10 до 3600 секунд";
                _aiSettings.TimeoutSeconds = timeout;
                return null;

            case "clipath":
                if (string.IsNullOrWhiteSpace(value))
                    return "CliPath не может быть пустым";
                _aiSettings.CliPath = value;
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
            ai["Model"]          = _aiSettings.Model;
            ai["MaxTurns"]       = _aiSettings.MaxTurns;
            ai["TimeoutSeconds"] = _aiSettings.TimeoutSeconds;
            ai["CliPath"]        = _aiSettings.CliPath;
            json["Ai"]           = ai;

            File.WriteAllText(_settingsPath,
                json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);
        }
        catch
        {
            // ignore — in-memory settings are already updated
        }
    }
}
