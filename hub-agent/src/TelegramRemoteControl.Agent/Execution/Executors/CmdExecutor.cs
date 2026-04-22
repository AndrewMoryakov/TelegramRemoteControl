using System.Text;
using TelegramRemoteControl.Agent.Interop;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class CmdExecutor : ICommandExecutor
{
    private readonly AgentSettings _settings;

    public CmdExecutor(AgentSettings settings)
    {
        _settings = settings;
    }

    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Arguments))
        {
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = "Укажите команду"
            };
        }

        string output;
        if (string.Equals(_settings.ShellRunAs, "User", StringComparison.OrdinalIgnoreCase))
        {
            // cmd redirection writes in the console OEM codepage (CP866 on RU Windows).
            var captured = await Task.Run(() => SessionInterop.RunInUserSessionCaptured(
                command.Arguments, timeoutMs: 60_000, outputEncoding: Encoding.GetEncoding(866)), ct);

            if (captured.Error != null && string.IsNullOrEmpty(captured.Stdout) && string.IsNullOrEmpty(captured.Stderr))
            {
                return new AgentResponse
                {
                    RequestId = command.RequestId,
                    Type = ResponseType.Error,
                    Success = false,
                    ErrorMessage = $"Не удалось выполнить в пользовательской сессии: {captured.Error}"
                };
            }

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(captured.Stdout)) sb.AppendLine(captured.Stdout);
            if (!string.IsNullOrWhiteSpace(captured.Stderr)) sb.AppendLine($"⚠️ Ошибки:\n{captured.Stderr}");
            if (captured.Error != null) sb.AppendLine($"⚠️ {captured.Error}");
            output = sb.Length > 0 ? sb.ToString() : "✅ Выполнено (нет вывода)";
        }
        else
        {
            output = await ShellHelper.RunAsync("cmd.exe", $"/c {command.Arguments}", ct);
        }

        return new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Text,
            Success = true,
            Text = output
        };
    }
}
