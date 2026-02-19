using System.Collections.Concurrent;

namespace TelegramRemoteControl.BotService;

public static class AiSessionManager
{
    private static readonly ConcurrentDictionary<long, AiSession> _sessions = new();

    public static bool IsActive(long userId) => _sessions.ContainsKey(userId);

    public static AiSession? Get(long userId) => _sessions.GetValueOrDefault(userId);

    public static AiSession Start(long userId)
    {
        var session = new AiSession();
        _sessions[userId] = session;
        return session;
    }

    public static void End(long userId) => _sessions.TryRemove(userId, out _);
}

public class AiSession
{
    public string? ClaudeSessionId { get; set; }
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastMessageAt { get; set; } = DateTimeOffset.UtcNow;
    public int MessageCount { get; set; }
    public SemaphoreSlim Lock { get; } = new(1, 1);
}
