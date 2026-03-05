using System.Collections.Concurrent;

namespace TelegramRemoteControl.BotService;

public static class WindowTypeSession
{
    private static readonly ConcurrentDictionary<long, WindowTypeState> _sessions = new();

    public static bool IsActive(long userId) => _sessions.ContainsKey(userId);

    public static WindowTypeState? Get(long userId) => _sessions.GetValueOrDefault(userId);

    public static void Start(long userId, long hwnd, long chatId) =>
        _sessions[userId] = new WindowTypeState(hwnd, chatId);

    public static void End(long userId) => _sessions.TryRemove(userId, out _);
}

public record WindowTypeState(long Hwnd, long ChatId);
