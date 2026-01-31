using System.ServiceProcess;
using System.Text.Json;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class ServiceActionExecutor : ICommandExecutor
{
    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        if (!TryParse(command, out var action, out var name, out var error))
        {
            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = error ?? "Неверные параметры"
            });
        }

        try
        {
            using var svc = new ServiceController(name);

            switch (action)
            {
                case "start":
                    if (svc.Status != ServiceControllerStatus.Running)
                    {
                        svc.Start();
                        svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    }
                    return Text(command.RequestId, $"▶️ Служба {name} запущена");

                case "stop":
                    if (svc.Status == ServiceControllerStatus.Running)
                    {
                        svc.Stop();
                        svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                    return Text(command.RequestId, $"⏹ Служба {name} остановлена");

                case "restart":
                    if (svc.Status == ServiceControllerStatus.Running)
                    {
                        svc.Stop();
                        svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                    svc.Start();
                    svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    return Text(command.RequestId, $"🔄 Служба {name} перезапущена");

                case "info":
                    svc.Refresh();
                    var info = new ServiceInfo
                    {
                        Name = svc.ServiceName,
                        DisplayName = svc.DisplayName,
                        Status = svc.Status.ToString(),
                        StartType = svc.StartType.ToString()
                    };
                    return Task.FromResult(new AgentResponse
                    {
                        RequestId = command.RequestId,
                        Type = ResponseType.Structured,
                        Success = true,
                        JsonPayload = JsonSerializer.Serialize(info)
                    });

                default:
                    return Task.FromResult(new AgentResponse
                    {
                        RequestId = command.RequestId,
                        Type = ResponseType.Error,
                        Success = false,
                        ErrorMessage = "Неизвестное действие"
                    });
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    private static bool TryParse(AgentCommand command, out string action, out string name, out string? error)
    {
        action = string.Empty;
        name = string.Empty;
        error = null;

        if (command.Parameters != null)
        {
            if (command.Parameters.TryGetValue("action", out var a) &&
                command.Parameters.TryGetValue("name", out var n))
            {
                action = a;
                name = n;
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(command.Arguments))
        {
            var parts = command.Arguments.Split(':', 2);
            if (parts.Length == 2)
            {
                action = parts[0];
                name = parts[1];
                return true;
            }
        }

        error = "Укажите action и serviceName";
        return false;
    }

    private static Task<AgentResponse> Text(string requestId, string text)
    {
        return Task.FromResult(new AgentResponse
        {
            RequestId = requestId,
            Type = ResponseType.Text,
            Success = true,
            Text = text
        });
    }

    private class ServiceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StartType { get; set; } = string.Empty;
    }
}
