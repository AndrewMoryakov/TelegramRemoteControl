using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;
using File = System.IO.File;
using TelegramRemoteControl.Helpers;
using TelegramRemoteControl.Interop;

namespace TelegramRemoteControl.Commands.Impl;

public class ScreenshotCommand : CommandBase
{
    private static readonly string DiagLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "TelegramRemoteControl", "screenshot_diag.log");

    public override string Id => "screenshot";
    public override string[] Aliases => new[] { "/screenshot", "/ss", "/screen" };
    public override string Title => "Скриншот";
    public override string Icon => "📸";
    public override string Description => "Скриншот экрана";
    public override string Category => Categories.Screen;

    // PowerShell script that captures the screen.
    // SetProcessDPIAware() is required so Screen.Bounds returns physical pixels
    // (otherwise at 125%/150% scaling the screenshot is cropped).
    // SystemInformation.VirtualScreen captures ALL monitors.
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

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        await SendAsync(ctx, "📸 Делаю скриншот...");

        // Use ProgramData for temp files — accessible to both Session 0 (service)
        // and user sessions. Path.GetTempPath() returns C:\Windows\SystemTemp for services,
        // which user-session processes may not be able to write to.
        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, $"screenshot_{Guid.NewGuid():N}.png");

        try
        {
            if (Process.GetCurrentProcess().SessionId == 0)
            {
                // Running as a Windows Service (Session 0) — no desktop access.
                // Launch PowerShell in the user's session to capture the screen.
                // Using powershell.exe avoids the .NET single-file extraction problem.
                var psScript = Path.Combine(tempDir, $"trc_screenshot_{Guid.NewGuid():N}.ps1");
                try
                {
                    File.WriteAllText(psScript, PsScreenshotScript);

                    var result = SessionInterop.RunInUserSession(
                        "powershell.exe",
                        $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\" \"{tempFile}\"");

                    WriteDiagLog(result, tempFile, psScript);

                    var screenshotExists = File.Exists(tempFile);
                    var screenshotOk = screenshotExists && new FileInfo(tempFile).Length > 0;

                    if (result.ExitCode != 0 && screenshotOk)
                    {
                        // If the screenshot file exists and has content, treat as success
                        // even when exit code reporting failed.
                    }
                    else
                    {
                        if (result.ExitCode == -1)
                            throw new InvalidOperationException("Нет активной пользовательской сессии");

                        if (result.ExitCode == -2)
                            throw new InvalidOperationException("Таймаут захвата экрана");

                        if (result.ExitCode != 0)
                            throw new InvalidOperationException(
                                $"PowerShell screenshot failed.\n{result.Dump()}");
                    }

                    if (!screenshotExists || !screenshotOk)
                        throw new InvalidOperationException(
                            $"Файл скриншота не создан.\n{result.Dump()}");
                }
                finally
                {
                    if (File.Exists(psScript)) File.Delete(psScript);
                }
            }
            else
            {
                // Interactive session — capture directly.
                ScreenshotHelper.CaptureAndSave(tempFile);
            }

            await using var stream = File.OpenRead(tempFile);
            await ctx.Bot.SendPhoto(
                ctx.ChatId,
                InputFile.FromStream(stream, "screenshot.png"),
                cancellationToken: ctx.CancellationToken);
        }
        catch (Exception ex)
        {
            await SendAsync(ctx, $"❌ Ошибка скриншота:\n```\n{ex.Message}\n```");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private static void WriteDiagLog(SessionInterop.RunResult result, string tempFile, string psScript)
    {
        try
        {
            var logDir = Path.GetDirectoryName(DiagLogPath);
            if (!string.IsNullOrEmpty(logDir))
                Directory.CreateDirectory(logDir);

            var lines = new[]
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Screenshot attempt",
                result.Dump(),
                $"PsScript: {psScript}",
                $"PsScriptExists: {File.Exists(psScript)}",
                $"TempFile: {tempFile}",
                $"TempFileExists: {File.Exists(tempFile)}",
                $"TempFileSize: {(File.Exists(tempFile) ? new FileInfo(tempFile).Length : 0)}",
                $"ServiceSession: {Process.GetCurrentProcess().SessionId}",
                "---"
            };
            File.AppendAllLines(DiagLogPath, lines);
        }
        catch { }
    }
}
