using System.Collections.Concurrent;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Hub.Services;

public class PendingCommandStore
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AgentResponse>> _pending = new();

    public async Task<AgentResponse> WaitForResponse(string requestId, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<AgentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = tcs;

        using var cts = new CancellationTokenSource(timeout);
        await using var reg = cts.Token.Register(() =>
            tcs.TrySetResult(new AgentResponse
            {
                RequestId = requestId,
                Type = ResponseType.Error,
                Success = false,
                ErrorMessage = "Таймаут ожидания ответа от агента"
            }));

        try
        {
            return await tcs.Task;
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    public bool Complete(string requestId, AgentResponse response)
    {
        if (_pending.TryRemove(requestId, out var tcs))
        {
            return tcs.TrySetResult(response);
        }
        return false;
    }
}
