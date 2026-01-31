using System.Diagnostics;
using System.Text.Json;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class ProcessesExecutor : ICommandExecutor
{
    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        // If argument is a process number — return info about that process (handled via JsonPayload)
        var processes = GetTopProcesses();

        var payload = processes.Select(p => new
        {
            pid = p.Pid,
            name = p.Name,
            ram = p.RamMb,
            cpu = Math.Round(p.CpuPercent, 1)
        });

        return Task.FromResult(new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Structured,
            Success = true,
            JsonPayload = JsonSerializer.Serialize(payload)
        });
    }

    private static List<ProcessInfo> GetTopProcesses()
    {
        var snapshot = new Dictionary<int, (string Name, long Ram, TimeSpan Cpu)>();
        var processes = Process.GetProcesses();

        foreach (var proc in processes)
        {
            try
            {
                snapshot[proc.Id] = (proc.ProcessName, proc.WorkingSet64, proc.TotalProcessorTime);
            }
            catch { }
        }

        var startTime = DateTime.UtcNow;
        Thread.Sleep(300);
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        var result = new List<ProcessInfo>();
        foreach (var proc in processes)
        {
            if (!snapshot.TryGetValue(proc.Id, out var start))
                continue;

            try
            {
                var endCpu = proc.TotalProcessorTime;
                var cpu = (endCpu - start.Cpu).TotalMilliseconds / elapsedMs / Environment.ProcessorCount * 100;

                result.Add(new ProcessInfo
                {
                    Pid = proc.Id,
                    Name = start.Name,
                    RamMb = (int)(start.Ram / 1024 / 1024),
                    CpuPercent = Math.Max(0, cpu)
                });
            }
            catch { }
        }

        return result
            .OrderByDescending(p => p.RamMb)
            .Take(50)
            .ToList();
    }

    private class ProcessInfo
    {
        public int Pid { get; set; }
        public string Name { get; set; } = "";
        public int RamMb { get; set; }
        public double CpuPercent { get; set; }
    }
}
