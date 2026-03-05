using System.Collections.Concurrent;

namespace TelegramRemoteControl.BotService;

public static class AiKeyInputSession
{
    private static readonly ConcurrentDictionary<long, long> _chatIds = new(); // userId → chatId

    public static bool IsActive(long userId) => _chatIds.ContainsKey(userId);

    public static void Start(long userId, long chatId) => _chatIds[userId] = chatId;

    public static void End(long userId) => _chatIds.TryRemove(userId, out _);
}
