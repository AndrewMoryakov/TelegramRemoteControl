using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class MonitorExecutor : ICommandExecutor
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var text = CollectSystemInfo();

        return Task.FromResult(new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Text,
            Success = true,
            Text = text
        });
    }

    private static string CollectSystemInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine("📈 Мониторинг системы\n");

        // CPU
        try
        {
            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue();
            Thread.Sleep(500);
            var cpuUsage = cpuCounter.NextValue();
            sb.AppendLine($"🔲 CPU: {cpuUsage:F1}%");
        }
        catch { sb.AppendLine("🔲 CPU: н/д"); }

        // CPU Frequency
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT CurrentClockSpeed, MaxClockSpeed FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                var current = Convert.ToInt32(obj["CurrentClockSpeed"]);
                var max = Convert.ToInt32(obj["MaxClockSpeed"]);
                sb.AppendLine($"⚡ Частота: {current} / {max} MHz");
                break;
            }
        }
        catch { }

        // RAM
        try
        {
            var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            GlobalMemoryStatusEx(ref mem);
            var totalGb = mem.ullTotalPhys / 1024.0 / 1024 / 1024;
            var usedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / 1024.0 / 1024 / 1024;
            sb.AppendLine($"💾 RAM: {usedGb:F1} / {totalGb:F1} GB ({mem.dwMemoryLoad}%)");
        }
        catch { sb.AppendLine("💾 RAM: н/д"); }

        // Disk
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var totalGb = drive.TotalSize / 1024.0 / 1024 / 1024;
                var freeGb = drive.AvailableFreeSpace / 1024.0 / 1024 / 1024;
                var usedPercent = (1 - drive.AvailableFreeSpace / (double)drive.TotalSize) * 100;
                sb.AppendLine($"💿 {drive.Name} {totalGb - freeGb:F0}/{totalGb:F0} GB ({usedPercent:F0}%)");
            }
        }
        catch { }

        // Network
        try
        {
            var category = new PerformanceCounterCategory("Network Interface");
            var instances = category.GetInstanceNames();
            if (instances.Length > 0)
            {
                var instance = instances.FirstOrDefault(i => !i.Contains("Loopback")) ?? instances[0];
                using var sentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instance);
                using var recvCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", instance);
                sentCounter.NextValue(); recvCounter.NextValue();
                Thread.Sleep(500);
                var sent = sentCounter.NextValue() / 1024;
                var recv = recvCounter.NextValue() / 1024;
                sb.AppendLine($"🌐 Сеть: ↑{sent:F0} ↓{recv:F0} KB/s");
            }
        }
        catch { }

        // Temperature
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
            foreach (var obj in searcher.Get())
            {
                var temp = (Convert.ToDouble(obj["CurrentTemperature"]) - 2732) / 10.0;
                sb.AppendLine($"🌡 Температура: {temp:F0}°C");
                break;
            }
        }
        catch
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\OpenHardwareMonitor", "SELECT * FROM Sensor WHERE SensorType='Temperature'");
                foreach (var obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString() ?? "";
                    var value = Convert.ToDouble(obj["Value"]);
                    if (name.Contains("CPU") || name.Contains("Package"))
                    {
                        sb.AppendLine($"🌡 {name}: {value:F0}°C");
                        break;
                    }
                }
            }
            catch { sb.AppendLine("🌡 Температура: требуется OpenHardwareMonitor"); }
        }

        // Uptime
        try
        {
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            sb.AppendLine($"\n⏱ Uptime: {uptime.Days}д {uptime.Hours}ч {uptime.Minutes}м");
        }
        catch { }

        return sb.ToString();
    }
}
