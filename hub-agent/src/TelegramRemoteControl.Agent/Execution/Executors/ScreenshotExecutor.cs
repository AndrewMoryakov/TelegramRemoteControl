using System.ComponentModel;
using System.Diagnostics;
using TelegramRemoteControl.Agent.Helpers;
using TelegramRemoteControl.Agent.Interop;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class ScreenshotExecutor : ICommandExecutor
{
    private const string PsScreenshotScript = @"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public class DpiUtil { [DllImport(""user32.dll"")] public static extern bool SetProcessDPIAware(); }'
[DpiUtil]::SetProcessDPIAware()
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
        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, $"screenshot_{Guid.NewGuid():N}.png");

        try
        {
            if (Process.GetCurrentProcess().SessionId == 0)
            {
                var psScript = Path.Combine(tempDir, $"trc_screenshot_{Guid.NewGuid():N}.ps1");
                try
                {
                    await File.WriteAllTextAsync(psScript, PsScreenshotScript, ct);

                    var result = SessionInterop.RunInUserSession(
                        "powershell.exe",
                        $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\" \"{tempFile}\"");

                    var screenshotOk = File.Exists(tempFile) && new FileInfo(tempFile).Length > 0;

                    if (result.ExitCode != 0 && !screenshotOk)
                    {
                        if (result.ExitCode == -1)
                            throw new InvalidOperationException("Нет активной пользовательской сессии");
                        if (result.ExitCode == -2)
                            throw new InvalidOperationException("Таймаут захвата экрана");
                        throw new InvalidOperationException($"Screenshot failed.\n{result.Dump()}");
                    }

                    if (!screenshotOk)
                        throw new InvalidOperationException($"Файл скриншота не создан.\n{result.Dump()}");
                }
                finally
                {
                    if (File.Exists(psScript)) File.Delete(psScript);
                }
            }
            else
            {
                ScreenshotHelper.CaptureAndSave(tempFile);
            }

            var data = await File.ReadAllBytesAsync(tempFile, ct);

            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Photo,
                Success = true,
                Data = data,
                FileName = "screenshot.png"
            };
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 6)
        {
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = "Скриншот недоступен: нет активного рабочего стола (сеанс заблокирован или агент запущен как сервис). Запустите агент в пользовательской сессии."
            };
        }
        catch (InvalidOperationException ex)
        {
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = $"Ошибка скриншота: {ex.Message}"
            };
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
