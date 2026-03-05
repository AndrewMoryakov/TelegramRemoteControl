using System.Collections.Concurrent;

namespace TelegramRemoteControl.BotService;

public enum ShellType { Cmd, PowerShell }

public record ShellState(ShellType Type);

public static class ShellSessionManager
{
    private static readonly ConcurrentDictionary<long, ShellState> _sessions = new();

    public static bool IsActive(long userId) => _sessions.ContainsKey(userId);

    public static ShellState? Get(long userId) => _sessions.GetValueOrDefault(userId);

    public static void Start(long userId, ShellType type) =>
        _sessions[userId] = new ShellState(type);

    public static void End(long userId) => _sessions.TryRemove(userId, out _);
}
