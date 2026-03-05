using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

/// <summary>
/// Routes AiChat commands to Claude CLI or OpenRouter based on current Provider setting.
/// </summary>
public class AiChatDispatcher : ICommandExecutor
{
    private readonly AiSettings _settings;
    private readonly AiChatExecutor _claude;
    private readonly OpenRouterChatExecutor _openRouter;

    public AiChatDispatcher(AiSettings settings)
    {
        _settings   = settings;
        _claude     = new AiChatExecutor(settings);
        _openRouter = new OpenRouterChatExecutor(settings);
    }

    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default) =>
        _settings.Provider == "openrouter"
            ? _openRouter.ExecuteAsync(command, ct)
            : _claude.ExecuteAsync(command, ct);
}
