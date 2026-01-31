using Microsoft.Extensions.Options;
using TelegramRemoteControl.Agent;
using TelegramRemoteControl.Agent.Execution.Executors;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution;

public class CommandExecutor
{
    private readonly Dictionary<CommandType, ICommandExecutor> _executors;
    private readonly ILogger<CommandExecutor> _logger;

    public CommandExecutor(ILogger<CommandExecutor> logger, IOptions<AgentSettings> settings)
    {
        _logger = logger;
        var agentSettings = settings.Value;
        _executors = new Dictionary<CommandType, ICommandExecutor>
        {
            [CommandType.Status] = new StatusExecutor(),
            [CommandType.Processes] = new ProcessesExecutor(),
            [CommandType.Drives] = new DrivesExecutor(),
            [CommandType.Ip] = new IpExecutor(),
            [CommandType.Monitor] = new MonitorExecutor(),
            [CommandType.Uptime] = new UptimeExecutor(),
            [CommandType.Cmd] = new CmdExecutor(),
            [CommandType.PowerShell] = new PowerShellExecutor(),
            [CommandType.Screenshot] = new ScreenshotExecutor(),
            [CommandType.WindowsList] = new WindowsListExecutor(),
            [CommandType.WindowAction] = new WindowActionExecutor(),
            [CommandType.WindowScreenshot] = new WindowScreenshotExecutor(),
            [CommandType.Kill] = new KillExecutor(),
            [CommandType.Services] = new ServicesExecutor(),
            [CommandType.ServiceAction] = new ServiceActionExecutor(),
            [CommandType.FileList] = new FileListExecutor(),
            [CommandType.FileDownload] = new FileDownloadExecutor(agentSettings),
            [CommandType.FilePreview] = new FilePreviewExecutor(agentSettings),
            [CommandType.Lock] = new LockExecutor(),
            [CommandType.Shutdown] = new ShutdownExecutor(),
            [CommandType.Restart] = new RestartExecutor(),
            [CommandType.Sleep] = new SleepExecutor(),
            [CommandType.Hibernate] = new HibernateExecutor(),
            // TODO: Этап 3.4 — добавить остальные executors
        };
    }

    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        if (!_executors.TryGetValue(command.Type, out var executor))
        {
            _logger.LogWarning("Unknown command type: {CommandType}", command.Type);
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = $"Неизвестная команда: {command.Type}"
            };
        }

        try
        {
            _logger.LogInformation("Executing command: {CommandType}, RequestId={RequestId}", command.Type, command.RequestId);
            return await executor.ExecuteAsync(command, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command {CommandType}", command.Type);
            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = $"Ошибка выполнения: {ex.Message}"
            };
        }
    }
}
