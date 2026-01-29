using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.Interop;
using File = System.IO.File;

namespace TelegramRemoteControl.Commands.Impl;

public class WindowsCommand : CommandBase
{
    public override string Id => "windows";
    public override string[] Aliases => new[] { "/windows", "/win" };
    public override string Title => "Окна";
    public override string Icon => "🪟";
    public override string Description => "Управление окнами";
    public override string Category => Categories.Screen;

    public record WindowInfo(long Hwnd, int Pid, string Title, string State);

    /// <summary>Cached window lists per chat</summary>
    private static readonly ConcurrentDictionary<long, List<WindowInfo>> Cache = new();

    // ── PowerShell: enumerate visible windows ──────────────────────────

    private const string PsEnumWindows = """
        Add-Type @"
        using System;
        using System.Collections.Generic;
        using System.Diagnostics;
        using System.Runtime.InteropServices;
        using System.Text;

        public class WinEnum {
            public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

            [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
            [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
            [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
            [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
            [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
            [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
            [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);

            public static List<string> GetWindows() {
                var result = new List<string>();
                EnumWindows((hWnd, _) => {
                    if (!IsWindowVisible(hWnd)) return true;
                    int len = GetWindowTextLength(hWnd);
                    if (len == 0) return true;
                    var sb = new StringBuilder(len + 1);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString();
                    if (string.IsNullOrWhiteSpace(title)) return true;
                    uint pid = 0;
                    GetWindowThreadProcessId(hWnd, out pid);
                    string state = IsIconic(hWnd) ? "minimized" : IsZoomed(hWnd) ? "maximized" : "normal";
                    result.Add(hWnd.ToInt64() + "|" + pid + "|" + title + "|" + state);
                    return true;
                }, IntPtr.Zero);
                return result;
            }
        }
        "@

        $outFile = $args[0]
        $lines = [WinEnum]::GetWindows()
        [System.IO.File]::WriteAllLines($outFile, $lines, [System.Text.Encoding]::UTF8)
        """;

    // ── PowerShell: window action (minimize/maximize/restore/close) ────

    private const string PsWindowAction = """
        Add-Type @"
        using System;
        using System.Runtime.InteropServices;

        public class WinAct {
            [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
            [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
            [DllImport("user32.dll", CharSet=CharSet.Auto)]
            public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
            [DllImport("user32.dll")]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

            public const int SW_MINIMIZE = 6;
            public const int SW_MAXIMIZE = 3;
            public const int SW_RESTORE  = 9;
            public const uint WM_CLOSE   = 0x0010;
            public const uint WM_DESTROY = 0x0002;

            public static bool DoAction(long hwnd, string action) {
                IntPtr h = new IntPtr(hwnd);
                if (!IsWindow(h)) return false;
                switch(action) {
                    case "min":     ShowWindow(h, SW_MINIMIZE); break;
                    case "max":     ShowWindow(h, SW_MAXIMIZE); break;
                    case "restore": ShowWindow(h, SW_RESTORE);  break;
                    case "close":
                        SendMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        // If still alive after WM_CLOSE, kill the process
                        System.Threading.Thread.Sleep(500);
                        if (IsWindow(h)) {
                            uint pid = 0;
                            GetWindowThreadProcessId(h, out pid);
                            if (pid != 0) {
                                try {
                                    var p = System.Diagnostics.Process.GetProcessById((int)pid);
                                    p.Kill();
                                } catch {}
                            }
                        }
                        break;
                    default: return false;
                }
                return true;
            }
        }
        "@

        $hwnd   = [long]$args[0]
        $action = $args[1]
        $result = [WinAct]::DoAction($hwnd, $action)
        if (-not $result) { exit 1 }
        """;

    // ── PowerShell: capture a single window screenshot ───────────────

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

        # DWM gives accurate bounds without shadow; fall back to GetWindowRect
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

    // ── PowerShell: minimize all ───────────────────────────────────────

    private const string PsMinimizeAll =
        "(New-Object -ComObject Shell.Application).MinimizeAll()";

    // ── Public helpers for callback handler ─────────────────────────────

    public static List<WindowInfo>? GetCached(long chatId) =>
        Cache.TryGetValue(chatId, out var list) ? list : null;

    public static async Task<List<WindowInfo>> EnumWindowsAsync(CancellationToken ct)
    {
        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);
        var resultFile = Path.Combine(tempDir, $"trc_winlist_{Guid.NewGuid():N}.txt");

        try
        {
            if (Process.GetCurrentProcess().SessionId == 0)
            {
                var psScript = Path.Combine(tempDir, $"trc_winenum_{Guid.NewGuid():N}.ps1");
                try
                {
                    File.WriteAllText(psScript, PsEnumWindows);
                    var result = SessionInterop.RunInUserSession(
                        "powershell.exe",
                        $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\" \"{resultFile}\"");

                    if (result.ExitCode == -1)
                        throw new InvalidOperationException("Нет активной пользовательской сессии");
                    if (result.ExitCode == -2)
                        throw new InvalidOperationException("Таймаут получения списка окон");
                }
                finally
                {
                    if (File.Exists(psScript)) File.Delete(psScript);
                }
            }
            else
            {
                // Interactive session — run PowerShell directly
                var psScript = Path.Combine(tempDir, $"trc_winenum_{Guid.NewGuid():N}.ps1");
                try
                {
                    File.WriteAllText(psScript, PsEnumWindows);
                    using var proc = new Process();
                    proc.StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\" \"{resultFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    proc.Start();
                    await proc.WaitForExitAsync(ct);
                }
                finally
                {
                    if (File.Exists(psScript)) File.Delete(psScript);
                }
            }

            if (!File.Exists(resultFile))
                throw new InvalidOperationException("Файл со списком окон не создан");

            var lines = await File.ReadAllLinesAsync(resultFile, Encoding.UTF8, ct);
            var windows = new List<WindowInfo>();
            foreach (var line in lines)
            {
                var parts = line.Split('|', 4);
                if (parts.Length < 4) continue;
                if (!long.TryParse(parts[0], out var hwnd)) continue;
                if (!int.TryParse(parts[1], out var pid)) continue;
                windows.Add(new WindowInfo(hwnd, pid, parts[2], parts[3]));
            }
            return windows;
        }
        finally
        {
            if (File.Exists(resultFile)) File.Delete(resultFile);
        }
    }

    public static void RunWindowAction(string action, long hwnd)
    {
        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);

        if (Process.GetCurrentProcess().SessionId == 0)
        {
            var psScript = Path.Combine(tempDir, $"trc_winact_{Guid.NewGuid():N}.ps1");
            try
            {
                File.WriteAllText(psScript, PsWindowAction);
                var result = SessionInterop.RunInUserSession(
                    "powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\" {hwnd} {action}");

                if (result.ExitCode == -1)
                    throw new InvalidOperationException("Нет активной пользовательской сессии");
                if (result.ExitCode == -2)
                    throw new InvalidOperationException("Таймаут выполнения действия");
                if (result.ExitCode != 0)
                    throw new InvalidOperationException("Окно не найдено или действие не выполнено");
            }
            finally
            {
                if (File.Exists(psScript)) File.Delete(psScript);
            }
        }
        else
        {
            var psScript = Path.Combine(tempDir, $"trc_winact_{Guid.NewGuid():N}.ps1");
            try
            {
                File.WriteAllText(psScript, PsWindowAction);
                using var proc = new Process();
                proc.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\" {hwnd} {action}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                proc.Start();
                proc.WaitForExit(10_000);
                if (proc.ExitCode != 0)
                    throw new InvalidOperationException("Окно не найдено или действие не выполнено");
            }
            finally
            {
                if (File.Exists(psScript)) File.Delete(psScript);
            }
        }
    }

    public static void RunMinimizeAll()
    {
        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);

        if (Process.GetCurrentProcess().SessionId == 0)
        {
            var psScript = Path.Combine(tempDir, $"trc_winminall_{Guid.NewGuid():N}.ps1");
            try
            {
                File.WriteAllText(psScript, PsMinimizeAll);
                SessionInterop.RunInUserSession(
                    "powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\"");
            }
            finally
            {
                if (File.Exists(psScript)) File.Delete(psScript);
            }
        }
        else
        {
            var psScript = Path.Combine(tempDir, $"trc_winminall_{Guid.NewGuid():N}.ps1");
            try
            {
                File.WriteAllText(psScript, PsMinimizeAll);
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
            finally
            {
                if (File.Exists(psScript)) File.Delete(psScript);
            }
        }
    }

    public static string CaptureWindow(long hwnd)
    {
        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);
        var outFile = Path.Combine(tempDir, $"trc_winss_{Guid.NewGuid():N}.png");

        if (Process.GetCurrentProcess().SessionId == 0)
        {
            var psScript = Path.Combine(tempDir, $"trc_winss_{Guid.NewGuid():N}.ps1");
            try
            {
                File.WriteAllText(psScript, PsWindowScreenshot);
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
            finally
            {
                if (File.Exists(psScript)) File.Delete(psScript);
            }
        }
        else
        {
            var psScript = Path.Combine(tempDir, $"trc_winss_{Guid.NewGuid():N}.ps1");
            try
            {
                File.WriteAllText(psScript, PsWindowScreenshot);
                using var proc = new Process();
                proc.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\" {hwnd} \"{outFile}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                proc.Start();
                proc.WaitForExit(15_000);
                if (proc.ExitCode != 0)
                    throw new InvalidOperationException("Не удалось захватить окно");
            }
            finally
            {
                if (File.Exists(psScript)) File.Delete(psScript);
            }
        }

        if (!File.Exists(outFile) || new FileInfo(outFile).Length == 0)
            throw new InvalidOperationException("Файл скриншота окна не создан");

        return outFile;
    }

    // ── Command execution ──────────────────────────────────────────────

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        // If argument given (e.g. /win 3) → show detail for that window
        if (!string.IsNullOrWhiteSpace(ctx.Arguments) && int.TryParse(ctx.Arguments.Trim(), out int index))
        {
            var cached = GetCached(ctx.ChatId);
            if (cached == null || index < 1 || index > cached.Count)
            {
                await SendAsync(ctx, "❌ Список окон устарел. Обновите командой /win");
                return;
            }

            var win = cached[index - 1];
            var stateIcon = win.State switch
            {
                "minimized" => "➖",
                "maximized" => "➕",
                _ => "🔲"
            };

            var text = $"""
                🪟 *Окно*

                📝 Заголовок: `{EscapeMarkdown(win.Title)}`
                🔢 HWND: `{win.Hwnd}`
                🔢 PID: `{win.Pid}`
                {stateIcon} Состояние: `{win.State}`
                """;

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📸 Скриншот", $"win:ss:{win.Hwnd}")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("➖ Свернуть", $"win:min:{win.Hwnd}"),
                    InlineKeyboardButton.WithCallbackData("➕ Развернуть", $"win:max:{win.Hwnd}")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔄 Восстановить", $"win:restore:{win.Hwnd}"),
                    InlineKeyboardButton.WithCallbackData("❌ Закрыть", $"win:close:{win.Hwnd}")
                },
                new[] { InlineKeyboardButton.WithCallbackData("◀️ К списку", "win:list") }
            });

            await ctx.Bot.SendMessage(ctx.ChatId, text,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        // Default: list windows
        await SendAsync(ctx, "🪟 Получаю список окон...");

        try
        {
            var windows = await EnumWindowsAsync(ctx.CancellationToken);
            Cache[ctx.ChatId] = windows;

            if (windows.Count == 0)
            {
                await SendAsync(ctx, "🪟 Нет открытых окон");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("🪟 *Окна*\n");

            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                var stateIcon = w.State switch
                {
                    "minimized" => "➖",
                    "maximized" => "➕",
                    _ => "🔲"
                };
                var title = w.Title.Length > 40 ? w.Title[..37] + "..." : w.Title;
                sb.AppendLine($"`{i + 1}.` {stateIcon} {EscapeMarkdown(title)}");
            }

            sb.AppendLine($"\n_Всего: {windows.Count}_");
            sb.AppendLine("_Нажмите кнопку или /win <номер>_");

            // Build inline buttons for first 10 windows
            var rows = new List<InlineKeyboardButton[]>();
            var buttonRow = new List<InlineKeyboardButton>();
            for (int i = 0; i < Math.Min(windows.Count, 10); i++)
            {
                var w = windows[i];
                var label = w.Title.Length > 20 ? w.Title[..17] + "..." : w.Title;
                buttonRow.Add(InlineKeyboardButton.WithCallbackData(
                    $"{i + 1}. {label}", $"win:info:{w.Hwnd}"));
                if (buttonRow.Count == 1)
                {
                    rows.Add(buttonRow.ToArray());
                    buttonRow = new List<InlineKeyboardButton>();
                }
            }
            if (buttonRow.Count > 0) rows.Add(buttonRow.ToArray());

            // Action buttons at bottom
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("➖ Свернуть все", "win:minall"),
                InlineKeyboardButton.WithCallbackData("❌ Закрыть все", "win:closeall")
            });
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 Обновить", "win:list"),
                InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu")
            });

            var keyboard = new InlineKeyboardMarkup(rows);
            await ctx.Bot.SendMessage(ctx.ChatId, sb.ToString(),
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: ctx.CancellationToken);
        }
        catch (Exception ex)
        {
            await SendAsync(ctx, $"❌ Ошибка:\n```\n{ex.Message}\n```");
        }
    }

    private static string EscapeMarkdown(string text) =>
        text.Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`").Replace("[", "\\[");
}
