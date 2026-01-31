using System.ServiceProcess;
using System.Text.Json;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class ServicesExecutor : ICommandExecutor
{
    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var services = ServiceController.GetServices()
            .OrderBy(s => s.DisplayName)
            .Select(s => new ServiceInfo
            {
                Name = s.ServiceName,
                DisplayName = s.DisplayName,
                Status = s.Status.ToString(),
                StartType = s.StartType.ToString()
            })
            .ToList();

        return Task.FromResult(new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Structured,
            Success = true,
            JsonPayload = JsonSerializer.Serialize(services)
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
