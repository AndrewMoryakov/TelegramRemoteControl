using System.Text;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class IpExecutor : ICommandExecutor
{
    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var result = await ShellHelper.RunAsync("powershell.exe",
            "-NoProfile -Command \"Get-NetIPAddress | Where-Object { $_.AddressFamily -eq 'IPv4' -and $_.IPAddress -ne '127.0.0.1' } | Select-Object IPAddress, InterfaceAlias | Format-Table -AutoSize\"",
            ct,
            Encoding.UTF8);

        return new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Text,
            Success = true,
            Text = $"🌐 IP адреса:\n```\n{result}\n```"
        };
    }
}
