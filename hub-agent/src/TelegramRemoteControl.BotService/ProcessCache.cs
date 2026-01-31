using System.Collections.Concurrent;
using TelegramRemoteControl.BotService.Models;

namespace TelegramRemoteControl.BotService;

internal static class ProcessCache
{
    private static readonly ConcurrentDictionary<long, CacheEntry> Cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public static void Set(long userId, List<ProcessInfo> items)
    {
        Cache[userId] = new CacheEntry(items, DateTimeOffset.UtcNow);
    }

    public static bool TryGet(long userId, out List<ProcessInfo> items)
    {
        items = new List<ProcessInfo>();
        if (!Cache.TryGetValue(userId, out var entry))
            return false;

        if (DateTimeOffset.UtcNow - entry.CreatedAt > Ttl)
        {
            Cache.TryRemove(userId, out _);
            return false;
        }

        items = entry.Items;
        return true;
    }

    private record CacheEntry(List<ProcessInfo> Items, DateTimeOffset CreatedAt);
}
