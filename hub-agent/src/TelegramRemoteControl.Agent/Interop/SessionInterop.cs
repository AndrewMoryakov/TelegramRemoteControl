using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TelegramRemoteControl.Agent.Interop;

/// <summary>
/// Launches a process in the active user's desktop session using Windows Task Scheduler.
/// Required when the caller runs in Session 0 (Windows Service).
/// </summary>
public static class SessionInterop
{
    private const int WTSActive = 0;
    private const int TokenUser = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct WTS_SESSION_INFO
    {
        public uint SessionId;
        [MarshalAs(UnmanagedType.LPStr)]
        public string pWinStationName;
        public int State;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(
        IntPtr hServer, int reserved, int version,
        out IntPtr ppSessionInfo, out int pCount);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pMemory);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass,
        IntPtr tokenInformation, int tokenInformationLength, out int returnLength);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupAccountSid(string? lpSystemName, IntPtr sid,
        StringBuilder lpName, ref int cchName,
        StringBuilder lpReferencedDomainName, ref int cchReferencedDomainName,
        out int peUse);

    public class RunResult
    {
        public int ExitCode { get; init; }
        public uint SessionId { get; init; }
        public string CommandLine { get; init; } = "";
        public string? WorkingDir { get; init; }
        public string? Error { get; init; }

        public string Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ExitCode: {ExitCode} (0x{unchecked((uint)ExitCode):X8})");
            sb.AppendLine($"Session: {SessionId}");
            sb.AppendLine($"CommandLine: {CommandLine}");
            sb.AppendLine($"WorkingDir: {WorkingDir}");
            if (Error != null) sb.AppendLine($"Error: {Error}");
            return sb.ToString();
        }
    }

    private static uint GetActiveUserSessionId()
    {
        uint consoleSession = WTSGetActiveConsoleSessionId();

        if (consoleSession != 0xFFFFFFFF && consoleSession != 0)
        {
            if (WTSQueryUserToken(consoleSession, out IntPtr token))
            {
                CloseHandle(token);
                return consoleSession;
            }
        }

        if (!WTSEnumerateSessions(IntPtr.Zero, 0, 1, out IntPtr pSessionInfo, out int count))
            return 0xFFFFFFFF;

        try
        {
            int structSize = Marshal.SizeOf<WTS_SESSION_INFO>();
            for (int i = 0; i < count; i++)
            {
                var si = Marshal.PtrToStructure<WTS_SESSION_INFO>(pSessionInfo + i * structSize);
                if (si.State == WTSActive && si.SessionId != 0)
                {
                    if (WTSQueryUserToken(si.SessionId, out IntPtr token))
                    {
                        CloseHandle(token);
                        return si.SessionId;
                    }
                }
            }
        }
        finally
        {
            WTSFreeMemory(pSessionInfo);
        }

        return 0xFFFFFFFF;
    }

    private static string? GetDomainUsername(IntPtr token)
    {
        GetTokenInformation(token, TokenUser, IntPtr.Zero, 0, out int needed);
        if (needed == 0) return null;

        var buffer = Marshal.AllocHGlobal(needed);
        try
        {
            if (!GetTokenInformation(token, TokenUser, buffer, needed, out _))
                return null;

            IntPtr sid = Marshal.ReadIntPtr(buffer);

            var nameBuilder = new StringBuilder(256);
            var domainBuilder = new StringBuilder(256);
            int nameLen = nameBuilder.Capacity;
            int domainLen = domainBuilder.Capacity;

            if (!LookupAccountSid(null, sid, nameBuilder, ref nameLen, domainBuilder, ref domainLen, out _))
                return null;

            var domain = domainBuilder.ToString();
            var name = nameBuilder.ToString();
            return string.IsNullOrEmpty(domain) ? name : $"{domain}\\{name}";
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string XmlEscape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
         .Replace("\"", "&quot;").Replace("'", "&apos;");

    private static string VbsEscape(string s) =>
        s.Replace("\"", "\"\"");

    private static (int ExitCode, string Output) RunLocalProcess(
        string fileName, string arguments, int timeoutMs = 10_000)
    {
        using var proc = new Process();
        proc.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        proc.Start();
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        if (!proc.WaitForExit(timeoutMs))
        {
            try { proc.Kill(); } catch { }
            return (-1, stdout + stderr + "\n[TIMEOUT]");
        }
        return (proc.ExitCode, stdout + stderr);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public static RunResult RunInUserSession(string exePath, string arguments, int timeoutMs = 15_000)
    {
        uint sessionId = GetActiveUserSessionId();
        string commandLine = $"\"{exePath}\" {arguments}";

        if (sessionId == 0xFFFFFFFF)
            return new RunResult
            {
                ExitCode = -1,
                SessionId = sessionId,
                CommandLine = commandLine,
                Error = "No active user session found"
            };

        IntPtr userToken = IntPtr.Zero;
        string? domainUser = null;
        try
        {
            if (WTSQueryUserToken(sessionId, out userToken))
                domainUser = GetDomainUsername(userToken);
        }
        finally
        {
            if (userToken != IntPtr.Zero) CloseHandle(userToken);
        }

        if (string.IsNullOrEmpty(domainUser))
            return new RunResult
            {
                ExitCode = -1,
                SessionId = sessionId,
                CommandLine = commandLine,
                Error = "Could not determine username for session"
            };

        var tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TelegramRemoteControl", "temp");
        Directory.CreateDirectory(tempDir);

        var taskId = Guid.NewGuid().ToString("N");
        var taskName = $"TRC_{taskId}";
        var exitCodeFile = Path.Combine(tempDir, $"{taskId}_exit.txt");
        var wrapperVbs = Path.Combine(tempDir, $"{taskId}.vbs");
        var xmlPath = Path.Combine(tempDir, $"{taskId}.xml");

        try
        {
            var vbsContent = new StringBuilder();
            vbsContent.AppendLine("Set WshShell = CreateObject(\"WScript.Shell\")");
            vbsContent.AppendLine($"exitCode = WshShell.Run(\"{VbsEscape(commandLine)}\", 0, True)");
            vbsContent.AppendLine("Set fso = CreateObject(\"Scripting.FileSystemObject\")");
            vbsContent.AppendLine($"Set f = fso.CreateTextFile(\"{VbsEscape(exitCodeFile)}\", True)");
            vbsContent.AppendLine("f.Write exitCode");
            vbsContent.AppendLine("f.Close");
            File.WriteAllText(wrapperVbs, vbsContent.ToString(), Encoding.ASCII);

            var timeoutSeconds = Math.Max(timeoutMs / 1000, 10);
            var xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <Principals>
    <Principal id=""Author"">
      <UserId>{XmlEscape(domainUser)}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT{timeoutSeconds}S</ExecutionTimeLimit>
    <Hidden>true</Hidden>
  </Settings>
  <Actions>
    <Exec>
      <Command>wscript.exe</Command>
      <Arguments>//B //NoLogo ""{XmlEscape(wrapperVbs)}""</Arguments>
    </Exec>
  </Actions>
</Task>";

            File.WriteAllText(xmlPath, xml, Encoding.Unicode);

            var (createEc, createOut) = RunLocalProcess("schtasks.exe",
                $"/Create /TN \"{taskName}\" /XML \"{xmlPath}\" /F");

            if (createEc != 0)
                return new RunResult
                {
                    ExitCode = -1,
                    SessionId = sessionId,
                    CommandLine = commandLine,
                    Error = $"schtasks /Create failed (exit {createEc}): {createOut.Trim()}"
                };

            try
            {
                var (runEc, runOut) = RunLocalProcess("schtasks.exe",
                    $"/Run /TN \"{taskName}\"");

                if (runEc != 0)
                    return new RunResult
                    {
                        ExitCode = -1,
                        SessionId = sessionId,
                        CommandLine = commandLine,
                        Error = $"schtasks /Run failed (exit {runEc}): {runOut.Trim()}"
                    };

                var deadline = Environment.TickCount64 + timeoutMs + 5000;
                string? lastExitFileContent = null;
                bool sawExitFile = false;
                while (Environment.TickCount64 < deadline)
                {
                    Thread.Sleep(300);

                    if (File.Exists(exitCodeFile))
                    {
                        sawExitFile = true;
                        Thread.Sleep(200);
                        try
                        {
                            var content = File.ReadAllText(exitCodeFile).Trim();
                            lastExitFileContent = content;
                            if (int.TryParse(content, out int ec))
                            {
                                return new RunResult
                                {
                                    ExitCode = ec,
                                    SessionId = sessionId,
                                    CommandLine = commandLine
                                };
                            }
                        }
                        catch { }
                    }
                }

                return new RunResult
                {
                    ExitCode = -2,
                    SessionId = sessionId,
                    CommandLine = commandLine,
                    Error = sawExitFile
                        ? $"Timeout waiting for valid exit code. Last content: '{lastExitFileContent ?? "<empty>"}'"
                        : "Timeout waiting for task completion"
                };
            }
            finally
            {
                RunLocalProcess("schtasks.exe", $"/Delete /TN \"{taskName}\" /F");
            }
        }
        finally
        {
            TryDelete(xmlPath);
            TryDelete(wrapperVbs);
            TryDelete(exitCodeFile);
        }
    }
}
