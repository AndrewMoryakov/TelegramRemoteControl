using System.Diagnostics;
using System.Text;
using TelegramRemoteControl.Agent.Interop;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class WindowActionExecutor : ICommandExecutor
{
    // Actions: min, max, restore, close, focus
    private const string PsWindowAction = """
        Add-Type @"
        using System;
        using System.Runtime.InteropServices;

        public class WinAct {
            [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
            [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
            [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
            [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
            [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
            [DllImport("user32.dll", CharSet=CharSet.Auto)]
            public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
            [DllImport("user32.dll")]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

            public const int SW_MINIMIZE = 6;
            public const int SW_MAXIMIZE = 3;
            public const int SW_RESTORE  = 9;
            public const uint WM_CLOSE   = 0x0010;

            public static bool DoAction(long hwnd, string action) {
                IntPtr h = new IntPtr(hwnd);
                if (!IsWindow(h)) return false;
                switch(action) {
                    case "min":     ShowWindow(h, SW_MINIMIZE); break;
                    case "max":     ShowWindow(h, SW_MAXIMIZE); break;
                    case "restore": ShowWindow(h, SW_RESTORE);  break;
                    case "focus":
                        if (IsIconic(h)) ShowWindow(h, SW_RESTORE);
                        BringWindowToTop(h);
                        SetForegroundWindow(h);
                        break;
                    case "close":
                        SendMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
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

    // Typed via SendInput with KEYEVENTF_UNICODE — works with any Unicode including Cyrillic
    private const string PsTypeAddType = """
        Add-Type @"
        using System;
        using System.Runtime.InteropServices;
        using System.Threading;

        public class WinTypeInput {
            [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
            [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int nCmd);
            [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
            [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
            [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
            [DllImport("user32.dll", SetLastError=true)]
            public static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);

            [StructLayout(LayoutKind.Explicit, Size=40)]
            public struct INPUT {
                [FieldOffset(0)] public int type;
                [FieldOffset(8)] public KEYBDINPUT ki;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct KEYBDINPUT {
                public ushort wVk;
                public ushort wScan;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            const int INPUT_KEYBOARD = 1;
            const uint KEYEVENTF_UNICODE = 0x0004;
            const uint KEYEVENTF_KEYUP   = 0x0002;

            public static void TypeText(long hwnd, string text) {
                IntPtr h = new IntPtr(hwnd);
                if (!IsWindow(h)) return;
                if (IsIconic(h)) ShowWindow(h, 9);
                BringWindowToTop(h);
                SetForegroundWindow(h);
                Thread.Sleep(200);
                var inputs = new INPUT[text.Length * 2];
                for (int i = 0; i < text.Length; i++) {
                    inputs[i * 2].type = INPUT_KEYBOARD;
                    inputs[i * 2].ki.wScan = (ushort)text[i];
                    inputs[i * 2].ki.dwFlags = KEYEVENTF_UNICODE;
                    inputs[i * 2 + 1].type = INPUT_KEYBOARD;
                    inputs[i * 2 + 1].ki.wScan = (ushort)text[i];
                    inputs[i * 2 + 1].ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
                }
                SendInput((uint)inputs.Length, inputs,
                    System.Runtime.InteropServices.Marshal.SizeOf(inputs[0]));
            }
        }
        "@
        """;

    // Special keys via VK code
    private const string PsKeyAddType = """
        Add-Type @"
        using System;
        using System.Runtime.InteropServices;
        using System.Threading;

        public class WinKeyInput {
            [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
            [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int nCmd);
            [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
            [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
            [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
            [DllImport("user32.dll", SetLastError=true)]
            public static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);

            [StructLayout(LayoutKind.Explicit, Size=40)]
            public struct INPUT {
                [FieldOffset(0)] public int type;
                [FieldOffset(8)] public KEYBDINPUT ki;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct KEYBDINPUT {
                public ushort wVk;
                public ushort wScan;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            const int INPUT_KEYBOARD = 1;
            const uint KEYEVENTF_KEYUP = 0x0002;

            public static void SendKey(long hwnd, ushort vk) {
                IntPtr h = new IntPtr(hwnd);
                if (!IsWindow(h)) return;
                if (IsIconic(h)) ShowWindow(h, 9);
                BringWindowToTop(h);
                SetForegroundWindow(h);
                Thread.Sleep(100);
                var inputs = new INPUT[2];
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].ki.wVk = vk;
                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].ki.wVk = vk;
                inputs[1].ki.dwFlags = KEYEVENTF_KEYUP;
                SendInput(2, inputs,
                    System.Runtime.InteropServices.Marshal.SizeOf(inputs[0]));
            }
        }
        "@
        """;

    private const string PsMinimizeAll =
        "(New-Object -ComObject Shell.Application).MinimizeAll()";

    private static readonly Dictionary<string, ushort> VirtualKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Enter"]    = 0x0D,
            ["Backspace"]= 0x08,
            ["Delete"]   = 0x2E,
            ["Escape"]   = 0x1B,
            ["Esc"]      = 0x1B,
            ["Tab"]      = 0x09,
            ["Up"]       = 0x26,
            ["Down"]     = 0x28,
            ["Left"]     = 0x25,
            ["Right"]    = 0x27,
            ["Home"]     = 0x24,
            ["End"]      = 0x23,
            ["PageUp"]   = 0x21,
            ["PageDown"] = 0x22,
            ["F5"]       = 0x74,
            ["Space"]    = 0x20,
        };

    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Arguments))
        {
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = "Укажите действие: min|max|restore|close|focus|minall|type|key и HWND"
            };
        }

        // Split into up to 3 parts so text after the second ':' is preserved intact
        // Formats: "minall" | "action:hwnd" | "type:hwnd:text" | "key:hwnd:keyname"
        var parts = command.Arguments.Split(':', 3);
        var action = parts[0];

        if (action == "minall")
        {
            RunMinimizeAll();
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = "✅ Все окна свёрнуты"
            };
        }

        if (parts.Length < 2 || !long.TryParse(parts[1], out var hwnd))
        {
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = "Неверный формат. Ожидается: action:hwnd"
            };
        }

        try
        {
            if (action == "type")
            {
                var text = parts.Length >= 3 ? parts[2] : string.Empty;
                if (string.IsNullOrEmpty(text))
                    return new AgentResponse
                    {
                        RequestId = command.RequestId,
                        Type = ResponseType.Error,
                        Success = false,
                        ErrorMessage = "Не указан текст для ввода"
                    };

                await RunTypeTextAsync(hwnd, text, ct);
                return new AgentResponse
                {
                    RequestId = command.RequestId,
                    Type = ResponseType.Text,
                    Success = true,
                    Text = "✅ Текст введён"
                };
            }

            if (action == "key")
            {
                var keyName = parts.Length >= 3 ? parts[2] : string.Empty;
                if (!VirtualKeys.TryGetValue(keyName, out var vkCode))
                    return new AgentResponse
                    {
                        RequestId = command.RequestId,
                        Type = ResponseType.Error,
                        Success = false,
                        ErrorMessage = $"Неизвестная клавиша: {keyName}"
                    };

                RunSendKey(hwnd, vkCode);
                return new AgentResponse
                {
                    RequestId = command.RequestId,
                    Type = ResponseType.Text,
                    Success = true,
                    Text = $"✅ Клавиша {keyName} нажата"
                };
            }

            // min / max / restore / close / focus
            RunWindowAction(action, hwnd);
        }
        catch (Exception ex)
        {
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = ex.Message
            };
        }

        var actionName = action switch
        {
            "min"     => "свёрнуто",
            "max"     => "развёрнуто",
            "restore" => "восстановлено",
            "close"   => "закрыто",
            "focus"   => "сфокусировано",
            _         => action
        };

        return new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Text,
            Success = true,
            Text = $"✅ Окно {actionName}"
        };
    }

    private static void RunWindowAction(string action, long hwnd)
    {
        var tempDir = GetTempDir();
        var psScript = Path.Combine(tempDir, $"trc_winact_{Guid.NewGuid():N}.ps1");
        try
        {
            File.WriteAllText(psScript, PsWindowAction);
            RunScript(psScript, $"{hwnd} {action}", expectSuccess: true);
        }
        finally
        {
            TryDelete(psScript);
        }
    }

    private static async Task RunTypeTextAsync(long hwnd, string text, CancellationToken ct)
    {
        var tempDir = GetTempDir();
        var textFile = Path.Combine(tempDir, $"trc_typetext_{Guid.NewGuid():N}.txt");
        var psScript = Path.Combine(tempDir, $"trc_type_{Guid.NewGuid():N}.ps1");
        try
        {
            await File.WriteAllTextAsync(textFile, text, Encoding.UTF8, ct);

            var safeTextFile = textFile.Replace("'", "''");
            var psContent = PsTypeAddType + $"""

                $text = [System.IO.File]::ReadAllText('{safeTextFile}', [System.Text.Encoding]::UTF8)
                [WinTypeInput]::TypeText({hwnd}L, $text)
                """;

            File.WriteAllText(psScript, psContent, Encoding.UTF8);
            RunScript(psScript, string.Empty, expectSuccess: false);
        }
        finally
        {
            TryDelete(textFile);
            TryDelete(psScript);
        }
    }

    private static void RunSendKey(long hwnd, ushort vkCode)
    {
        var tempDir = GetTempDir();
        var psScript = Path.Combine(tempDir, $"trc_key_{Guid.NewGuid():N}.ps1");
        try
        {
            var psContent = PsKeyAddType + $"""

                [WinKeyInput]::SendKey({hwnd}L, {vkCode})
                """;

            File.WriteAllText(psScript, psContent, Encoding.UTF8);
            RunScript(psScript, string.Empty, expectSuccess: false);
        }
        finally
        {
            TryDelete(psScript);
        }
    }

    private static void RunMinimizeAll()
    {
        var tempDir = GetTempDir();
        var psScript = Path.Combine(tempDir, $"trc_winminall_{Guid.NewGuid():N}.ps1");
        try
        {
            File.WriteAllText(psScript, PsMinimizeAll);
            RunScript(psScript, string.Empty, expectSuccess: false);
        }
        finally
        {
            TryDelete(psScript);
        }
    }

    /// <summary>Runs a PS1 script in the user session (via schtasks if Session 0) or directly.</summary>
    private static void RunScript(string psScript, string extraArgs, bool expectSuccess)
    {
        var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\"";
        if (!string.IsNullOrEmpty(extraArgs))
            args += $" {extraArgs}";

        if (Process.GetCurrentProcess().SessionId == 0)
        {
            var result = SessionInterop.RunInUserSession("powershell.exe", args);
            if (result.ExitCode == -1)
                throw new InvalidOperationException("Нет активной пользовательской сессии");
            if (result.ExitCode == -2)
                throw new InvalidOperationException("Таймаут выполнения");
            if (expectSuccess && result.ExitCode != 0)
                throw new InvalidOperationException("Окно не найдено или действие не выполнено");
        }
        else
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            proc.Start();
            proc.WaitForExit(10_000);
            if (expectSuccess && proc.ExitCode != 0)
                throw new InvalidOperationException("Окно не найдено или действие не выполнено");
        }
    }

    private static string GetTempDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
