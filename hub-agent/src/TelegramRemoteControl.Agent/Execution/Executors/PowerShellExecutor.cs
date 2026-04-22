using System.Text;
using TelegramRemoteControl.Agent.Interop;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class PowerShellExecutor : ICommandExecutor
{
    private readonly AgentSettings _settings;

    public PowerShellExecutor(AgentSettings settings)
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

        // -EncodedCommand принимает Base64(UTF-16LE) — исключает shell-escaping и инъекции через метасимволы
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command.Arguments));
        var psArgs = $"-NoProfile -NonInteractive -EncodedCommand {encoded}";

        string output;
        if (string.Equals(_settings.ShellRunAs, "User", StringComparison.OrdinalIgnoreCase))
        {
            var captured = await Task.Run(() => SessionInterop.RunInUserSessionCaptured(
                $"powershell.exe {psArgs}", timeoutMs: 60_000, outputEncoding: Encoding.UTF8), ct);

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
            output = await ShellHelper.RunAsync("powershell.exe", psArgs, ct, Encoding.UTF8);
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
