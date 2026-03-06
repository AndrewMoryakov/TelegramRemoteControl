using System.Diagnostics;
using System.IO.Compression;
using TelegramRemoteControl.Agent.Interop;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class ScreenRecordExecutor : ICommandExecutor
{
    private const string PsScreenshotScript = @"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public class DpiUtil2 { [DllImport(""user32.dll"")] public static extern bool SetProcessDPIAware(); }'
[DpiUtil2]::SetProcessDPIAware()
$bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$bmp.Save($args[0], [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
";

    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var seconds = 5;
        if (!string.IsNullOrWhiteSpace(command.Arguments) &&
            int.TryParse(command.Arguments.Trim(), out var parsed))
        {
            seconds = Math.Clamp(parsed, 1, 30);
        }

        var fps = 2;
        var frameCount = seconds * fps;
        var delayMs = 1000 / fps;

        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);

        var frameFiles = new List<string>();
        var isService = Process.GetCurrentProcess().SessionId == 0;

        try
        {
            // Capture frames
            for (int i = 0; i < frameCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var framePath = Path.Combine(tempDir, $"frame_{i:D4}.png");
                frameFiles.Add(framePath);

                if (isService)
                {
                    var psScript = Path.Combine(tempDir, $"trc_rec_{Guid.NewGuid():N}.ps1");
                    try
                    {
                        await File.WriteAllTextAsync(psScript, PsScreenshotScript, ct);
                        var result = SessionInterop.RunInUserSession(
                            "powershell.exe",
                            $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\" \"{framePath}\"",
                            timeoutMs: 10_000);

                        if (result.ExitCode == -1)
                            return Error(command, "Нет активной пользовательской сессии");
                    }
                    finally
                    {
                        TryDelete(psScript);
                    }
                }
                else
                {
                    Helpers.ScreenshotHelper.CaptureAndSave(framePath);
                }

                if (i < frameCount - 1)
                    await Task.Delay(delayMs, ct);
            }

            // Build ZIP
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (framePath, idx) in frameFiles.Select((p, i) => (p, i)))
                {
                    if (!File.Exists(framePath)) continue;
                    var entryName = $"frame_{idx + 1:D4}.png";
                    var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    var frameBytes = await File.ReadAllBytesAsync(framePath, ct);
                    await entryStream.WriteAsync(frameBytes, ct);
                }
            }

            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Document,
                Success = true,
                Data = ms.ToArray(),
                FileName = "recording.zip",
                Text = $"Запись экрана: {seconds}с, {frameFiles.Count} кадров"
            };
        }
        catch (OperationCanceledException)
        {
            return Error(command, "Запись отменена");
        }
        catch (Exception ex)
        {
            return Error(command, $"Ошибка записи экрана: {ex.Message}");
        }
        finally
        {
            foreach (var f in frameFiles)
                TryDelete(f);
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
