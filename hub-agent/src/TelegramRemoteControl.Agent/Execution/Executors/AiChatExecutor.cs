using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class AiChatExecutor : ICommandExecutor
{
    private readonly AiSettings _settings;

    public AiChatExecutor(AiSettings settings)
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
                ErrorMessage = "Укажите текст запроса"
            };
        }

        string? tempFile = null;
        try
        {
            // Write prompt to temp file to avoid shell escaping issues
            tempFile = Path.Combine(Path.GetTempPath(), $"claude-prompt-{Guid.NewGuid()}.txt");
            await File.WriteAllTextAsync(tempFile, command.Arguments, Encoding.UTF8, ct);

            // Build arguments
            var args = new StringBuilder();
            args.Append("--print --output-format json");
            args.Append($" --max-turns {_settings.MaxTurns}");

            // Resume session if sessionId provided
            string? sessionId = null;
            command.Parameters?.TryGetValue("sessionId", out sessionId);
            if (!string.IsNullOrEmpty(sessionId))
                args.Append($" --resume \"{sessionId}\"");

            // Use cmd.exe /c type to pipe file content to claude
            var cmdArgs = $"/c type \"{tempFile}\" | \"{_settings.CliPath}\" {args}";

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
            var linkedToken = timeoutCts.Token;

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = cmdArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedToken);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedToken);

            await process.WaitForExitAsync(linkedToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
            {
                var errorText = !string.IsNullOrWhiteSpace(stderr) ? stderr : $"Claude CLI завершился с кодом {process.ExitCode}";
                return new AgentResponse
                {
                    RequestId = command.RequestId,
                    Type = ResponseType.Error,
                    Success = false,
                    ErrorMessage = errorText
                };
            }

            // Parse JSON output
            ClaudeCliResult? result = null;
            try
            {
                result = JsonSerializer.Deserialize<ClaudeCliResult>(stdout);
            }
            catch
            {
                // If JSON parsing fails, return raw output
                return new AgentResponse
                {
                    RequestId = command.RequestId,
                    Type = ResponseType.Text,
                    Success = true,
                    Text = stdout.Trim()
                };
            }

            if (result == null)
            {
                return new AgentResponse
                {
                    RequestId = command.RequestId,
                    Type = ResponseType.Text,
                    Success = true,
                    Text = stdout.Trim()
                };
            }

            if (result.IsError)
            {
                return new AgentResponse
                {
                    RequestId = command.RequestId,
                    Type = ResponseType.Error,
                    Success = false,
                    ErrorMessage = result.Result ?? "Claude вернул ошибку"
                };
            }

            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = result.Result ?? "Нет ответа",
                JsonPayload = stdout.Trim()
            };
        }
        catch (Win32Exception)
        {
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = "Claude CLI не найден. Убедитесь, что claude установлен и доступен в PATH."
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = $"AI запрос отменен по таймауту ({_settings.TimeoutSeconds}с)"
            };
        }
        finally
        {
            if (tempFile != null)
            {
                try { File.Delete(tempFile); } catch { /* ignore */ }
            }
        }
    }
}
