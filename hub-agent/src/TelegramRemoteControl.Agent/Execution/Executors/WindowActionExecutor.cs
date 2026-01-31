using System.Diagnostics;
using TelegramRemoteControl.Agent.Interop;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class WindowActionExecutor : ICommandExecutor
{
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

            public static bool DoAction(long hwnd, string action) {
                IntPtr h = new IntPtr(hwnd);
                if (!IsWindow(h)) return false;
                switch(action) {
                    case "min":     ShowWindow(h, SW_MINIMIZE); break;
                    case "max":     ShowWindow(h, SW_MAXIMIZE); break;
                    case "restore": ShowWindow(h, SW_RESTORE);  break;
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

    private const string PsMinimizeAll =
        "(New-Object -ComObject Shell.Application).MinimizeAll()";

    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Arguments))
        {
            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = "Укажите действие: min|max|restore|close|minall и HWND"
            });
        }

        // Arguments format: "action:hwnd" e.g. "min:12345" or "minall"
        var parts = command.Arguments.Split(':', 2);
        var action = parts[0];

        if (action == "minall")
        {
            RunMinimizeAll();
            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = "✅ Все окна свёрнуты"
            });
        }

        if (parts.Length < 2 || !long.TryParse(parts[1], out var hwnd))
        {
            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = "Неверный формат. Ожидается: action:hwnd"
            });
        }

        RunWindowAction(action, hwnd);

        var actionName = action switch
        {
            "min" => "свёрнуто",
            "max" => "развёрнуто",
            "restore" => "восстановлено",
            "close" => "закрыто",
            _ => action
        };

        return Task.FromResult(new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Text,
            Success = true,
            Text = $"✅ Окно {actionName}"
        });
    }

    private static void RunWindowAction(string action, long hwnd)
    {
        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);

        var psScript = Path.Combine(tempDir, $"trc_winact_{Guid.NewGuid():N}.ps1");
        try
        {
            File.WriteAllText(psScript, PsWindowAction);

            if (Process.GetCurrentProcess().SessionId == 0)
            {
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
            else
            {
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
        }
        finally
        {
            if (File.Exists(psScript)) File.Delete(psScript);
        }
    }

    private static void RunMinimizeAll()
    {
        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);

        var psScript = Path.Combine(tempDir, $"trc_winminall_{Guid.NewGuid():N}.ps1");
        try
        {
            File.WriteAllText(psScript, PsMinimizeAll);

            if (Process.GetCurrentProcess().SessionId == 0)
            {
                SessionInterop.RunInUserSession(
                    "powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\"");
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
        }
        finally
        {
            if (File.Exists(psScript)) File.Delete(psScript);
        }
    }
}
