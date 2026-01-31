using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class DrivesExecutor : ICommandExecutor
{
    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => $"{d.Name} {d.DriveType}: {d.AvailableFreeSpace / 1024 / 1024 / 1024} GB свободно из {d.TotalSize / 1024 / 1024 / 1024} GB");

        var text = $"💾 Диски:\n```\n{string.Join("\n", drives)}\n```";

        return Task.FromResult(new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Text,
            Success = true,
            Text = text
        });
    }
}
