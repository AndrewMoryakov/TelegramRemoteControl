using System.Diagnostics;
using TelegramRemoteControl.Agent.Interop;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class WindowScreenshotExecutor : ICommandExecutor
{
    private const string PsWindowScreenshot = """
        Add-Type -AssemblyName System.Drawing
        Add-Type -TypeDefinition @"
        using System;
        using System.Runtime.InteropServices;

        public class WinCapture {
            [StructLayout(LayoutKind.Sequential)]
            public struct RECT { public int Left, Top, Right, Bottom; }

            [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
            [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
            [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
            [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
            [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
            [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
            [DllImport("dwmapi.dll")]
            public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT pvAttribute, int cbAttribute);

            public const int SW_RESTORE = 9;
            public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        }
        "@

        $hwnd    = [long]$args[0]
        $outFile = $args[1]
        $h = [IntPtr]::new($hwnd)

        if (-not [WinCapture]::IsWindow($h)) { exit 1 }

        [WinCapture]::SetProcessDPIAware()

        if ([WinCapture]::IsIconic($h)) {
            [WinCapture]::ShowWindow($h, [WinCapture]::SW_RESTORE)
            Start-Sleep -Milliseconds 300
        }
        [WinCapture]::SetForegroundWindow($h)
        Start-Sleep -Milliseconds 200

        $r = New-Object WinCapture+RECT
        $hr = [WinCapture]::DwmGetWindowAttribute($h, [WinCapture]::DWMWA_EXTENDED_FRAME_BOUNDS, [ref]$r, [System.Runtime.InteropServices.Marshal]::SizeOf($r))
        if ($hr -ne 0) {
            if (-not [WinCapture]::GetWindowRect($h, [ref]$r)) { exit 1 }
        }

        $w = $r.Right - $r.Left
        $ht = $r.Bottom - $r.Top
        if ($w -le 0 -or $ht -le 0) { exit 1 }

        $bmp = New-Object System.Drawing.Bitmap($w, $ht)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $ht)))
        $bmp.Save($outFile, [System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose()
        $bmp.Dispose()
        """;

    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Arguments) || !long.TryParse(command.Arguments, out var hwnd))
        {
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = "Укажите HWND окна"
            };
        }

        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);
        var outFile = Path.Combine(tempDir, $"trc_winss_{Guid.NewGuid():N}.png");

        try
        {
            var psScript = Path.Combine(tempDir, $"trc_winss_{Guid.NewGuid():N}.ps1");
            try
            {
                await File.WriteAllTextAsync(psScript, PsWindowScreenshot, ct);

                if (Process.GetCurrentProcess().SessionId == 0)
                {
                    var result = SessionInterop.RunInUserSession(
                        "powershell.exe",
                        $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\" {hwnd} \"{outFile}\"");

                    if (result.ExitCode == -1)
                        throw new InvalidOperationException("Нет активной пользовательской сессии");
                    if (result.ExitCode == -2)
                        throw new InvalidOperationException("Таймаут захвата окна");
                    if (result.ExitCode != 0 && !(File.Exists(outFile) && new FileInfo(outFile).Length > 0))
                        throw new InvalidOperationException("Не удалось захватить окно");
                }
                else
                {
                    using var proc = new Process();
                    proc.StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\" {hwnd} \"{outFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    proc.Start();
                    await proc.WaitForExitAsync(ct);
                    if (proc.ExitCode != 0)
                        throw new InvalidOperationException("Не удалось захватить окно");
                }
            }
            finally
            {
                if (File.Exists(psScript)) File.Delete(psScript);
            }

            if (!File.Exists(outFile) || new FileInfo(outFile).Length == 0)
                throw new InvalidOperationException("Файл скриншота окна не создан");

            var data = await File.ReadAllBytesAsync(outFile, ct);

            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Photo,
                Success = true,
                Data = data,
                FileName = "window.png"
            };
        }
        finally
        {
            if (File.Exists(outFile)) File.Delete(outFile);
        }
    }
}
