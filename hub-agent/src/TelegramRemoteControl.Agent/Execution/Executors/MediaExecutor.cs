using System.Diagnostics;
using TelegramRemoteControl.Agent.Interop;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class MediaExecutor : ICommandExecutor
{
    private static readonly Dictionary<string, (byte Vk, string Label)> MediaKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["play"]    = (0xB3, "⏯ Пауза/Воспроизведение"),
            ["next"]    = (0xB0, "⏭ Следующий трек"),
            ["prev"]    = (0xB1, "⏮ Предыдущий трек"),
            ["volup"]   = (0xAF, "🔊 Громче"),
            ["voldown"] = (0xAE, "🔉 Тише"),
            ["mute"]    = (0xAD, "🔇 Без звука"),
        };

    private const string PsMediaKeyTemplate = """
        Add-Type @"
        using System;
        using System.Runtime.InteropServices;
        public class MediaKeyHelper {{
            [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, int extra);
        }}
        "@
        [MediaKeyHelper]::keybd_event({0}, 0, 0, 0)
        [MediaKeyHelper]::keybd_event({0}, 0, 2, 0)
        """;

    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var action = command.Arguments?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!MediaKeys.TryGetValue(action, out var key))
            return Error(command, $"Неизвестное действие: {action}. Доступно: play, next, prev, volup, voldown, mute");

        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);

        var psScript = Path.Combine(tempDir, $"trc_media_{Guid.NewGuid():N}.ps1");
        try
        {
            var script = string.Format(PsMediaKeyTemplate, key.Vk);
            await File.WriteAllTextAsync(psScript, script, ct);

            if (Process.GetCurrentProcess().SessionId == 0)
            {
                var result = SessionInterop.RunInUserSession(
                    "powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\"");

                if (result.ExitCode == -1)
                    return Error(command, "Нет активной пользовательской сессии");
                if (result.ExitCode == -2)
                    return Error(command, "Таймаут выполнения");
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
                proc.WaitForExit(5_000);
            }

            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = $"✅ {key.Label}"
            };
        }
        catch (Exception ex)
        {
            return Error(command, $"Ошибка управления медиа: {ex.Message}");
        }
        finally
        {
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
