using System.Diagnostics;
using TelegramRemoteControl.Agent.Interop;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class ClipboardGetExecutor : ICommandExecutor
{
    private const string PsGetClipboard = """
        Add-Type -AssemblyName System.Windows.Forms
        $text = [System.Windows.Forms.Clipboard]::GetText()
        Write-Output $text
        """;

    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);

        var psScript = Path.Combine(tempDir, $"trc_clipget_{Guid.NewGuid():N}.ps1");
        var outFile = Path.Combine(tempDir, $"trc_clipout_{Guid.NewGuid():N}.txt");
        try
        {
            var script = PsGetClipboard + $"\n$text | Out-File -FilePath '{outFile.Replace("'", "''")}' -Encoding utf8 -NoNewline";
            await File.WriteAllTextAsync(psScript, script, ct);

            if (Process.GetCurrentProcess().SessionId == 0)
            {
                var result = SessionInterop.RunInUserSession(
                    "powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\"");

                if (result.ExitCode == -1)
                    return Error(command, "Нет активной пользовательской сессии");
                if (result.ExitCode == -2)
                    return Error(command, "Таймаут чтения буфера обмена");
            }
            else
            {
                using var proc = new Process();
                proc.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                proc.Start();
                proc.WaitForExit(10_000);
            }

            var text = File.Exists(outFile)
                ? await File.ReadAllTextAsync(outFile, ct)
                : string.Empty;

            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = string.IsNullOrEmpty(text) ? "(буфер обмена пуст)" : $"📋 Буфер обмена:\n{text}"
            };
        }
        catch (Exception ex)
        {
            return Error(command, $"Ошибка чтения буфера: {ex.Message}");
        }
        finally
        {
            TryDelete(psScript);
            TryDelete(outFile);
        }
    }

    private static AgentResponse Error(AgentCommand command, string message) => new()
    {
        RequestId = command.RequestId,
        Type = ResponseType.Error,
        Success = false,
        ErrorMessage = message
    };

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
