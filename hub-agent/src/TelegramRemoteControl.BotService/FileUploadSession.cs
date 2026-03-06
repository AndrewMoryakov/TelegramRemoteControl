using System.Collections.Concurrent;

namespace TelegramRemoteControl.BotService;

public static class FileUploadSession
{
    private static readonly ConcurrentDictionary<long, string?> _sessions = new(); // userId → destinationPath

    public static bool IsActive(long userId) => _sessions.ContainsKey(userId);

    public static void Start(long userId, string? destinationPath) => _sessions[userId] = destinationPath;

    public static string? GetDestination(long userId) => _sessions.TryGetValue(userId, out var path) ? path : null;

    public static void End(long userId) => _sessions.TryRemove(userId, out _);
}
