using System.Diagnostics;
using System.Text;
using TelegramRemoteControl.Agent.Interop;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class ClipboardSetExecutor : ICommandExecutor
{
    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(command.Arguments))
            return Error(command, "Укажите текст для вставки в буфер обмена");

        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);

        var textFile = Path.Combine(tempDir, $"trc_clipin_{Guid.NewGuid():N}.txt");
        var psScript = Path.Combine(tempDir, $"trc_clipset_{Guid.NewGuid():N}.ps1");
        try
        {
            await File.WriteAllTextAsync(textFile, command.Arguments, Encoding.UTF8, ct);

            var safeTextFile = textFile.Replace("'", "''");
            var script = $"""
                Add-Type -AssemblyName System.Windows.Forms
                $text = [System.IO.File]::ReadAllText('{safeTextFile}', [System.Text.Encoding]::UTF8)
                [System.Windows.Forms.Clipboard]::SetText($text)
                """;
            await File.WriteAllTextAsync(psScript, script, ct);

            if (Process.GetCurrentProcess().SessionId == 0)
            {
                var result = SessionInterop.RunInUserSession(
                    "powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\"");

                if (result.ExitCode == -1)
                    return Error(command, "Нет активной пользовательской сессии");
                if (result.ExitCode == -2)
                    return Error(command, "Таймаут записи в буфер обмена");
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

            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = "✅ Текст скопирован в буфер обмена"
            };
        }
        catch (Exception ex)
        {
            return Error(command, $"Ошибка записи в буфер: {ex.Message}");
        }
        finally
        {
            TryDelete(textFile);
            TryDelete(psScript);
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
