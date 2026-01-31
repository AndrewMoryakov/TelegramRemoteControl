using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TelegramRemoteControl.Agent.Interop;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class WindowsListExecutor : ICommandExecutor
{
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

    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var windows = await EnumWindowsAsync(ct);

        if (windows.Count == 0)
        {
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = "🪟 Нет открытых окон"
            };
        }

        var sb = new StringBuilder();
        sb.AppendLine("🪟 Окна\n");

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
            sb.AppendLine($"{i + 1}. {stateIcon} {title}");
        }

        sb.AppendLine($"\nВсего: {windows.Count}");

        // Build buttons for first 10 windows
        var buttons = new List<ButtonRow>();
        for (int i = 0; i < Math.Min(windows.Count, 10); i++)
        {
            var w = windows[i];
            var label = w.Title.Length > 20 ? w.Title[..17] + "..." : w.Title;
            buttons.Add(new ButtonRow
            {
                Buttons = new List<ButtonInfo>
                {
                    new() { Text = $"{i + 1}. {label}", CallbackData = $"win:info:{w.Hwnd}" }
                }
            });
        }

        buttons.Add(new ButtonRow
        {
            Buttons = new List<ButtonInfo>
            {
                new() { Text = "➖ Свернуть все", CallbackData = "win:minall" },
                new() { Text = "🔄 Обновить", CallbackData = "win:list" }
            }
        });

        // Store window list in JsonPayload for BotService to use
        var jsonPayload = JsonSerializer.Serialize(windows);

        return new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Text,
            Success = true,
            Text = sb.ToString(),
            Buttons = buttons,
            JsonPayload = jsonPayload
        };
    }

    public static async Task<List<WindowData>> EnumWindowsAsync(CancellationToken ct)
    {
        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);
        var resultFile = Path.Combine(tempDir, $"trc_winlist_{Guid.NewGuid():N}.txt");

        try
        {
            var psScript = Path.Combine(tempDir, $"trc_winenum_{Guid.NewGuid():N}.ps1");
            try
            {
                await File.WriteAllTextAsync(psScript, PsEnumWindows, ct);

                if (Process.GetCurrentProcess().SessionId == 0)
                {
                    var result = SessionInterop.RunInUserSession(
                        "powershell.exe",
                        $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\" \"{resultFile}\"");

                    if (result.ExitCode == -1)
                        throw new InvalidOperationException("Нет активной пользовательской сессии");
                    if (result.ExitCode == -2)
                        throw new InvalidOperationException("Таймаут получения списка окон");
                }
                else
                {
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
            }
            finally
            {
                if (File.Exists(psScript)) File.Delete(psScript);
            }

            if (!File.Exists(resultFile))
                throw new InvalidOperationException("Файл со списком окон не создан");

            var lines = await File.ReadAllLinesAsync(resultFile, Encoding.UTF8, ct);
            var windows = new List<WindowData>();
            foreach (var line in lines)
            {
                var parts = line.Split('|', 4);
                if (parts.Length < 4) continue;
                if (!long.TryParse(parts[0], out var hwnd)) continue;
                if (!int.TryParse(parts[1], out var pid)) continue;
                windows.Add(new WindowData { Hwnd = hwnd, Pid = pid, Title = parts[2], State = parts[3] });
            }
            return windows;
        }
        finally
        {
            if (File.Exists(resultFile)) File.Delete(resultFile);
        }
    }

    public class WindowData
    {
        public long Hwnd { get; set; }
        public int Pid { get; set; }
        public string Title { get; set; } = "";
        public string State { get; set; } = "";
    }
}
