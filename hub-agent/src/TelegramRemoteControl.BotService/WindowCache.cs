using System.Collections.Concurrent;
using TelegramRemoteControl.BotService.Models;

namespace TelegramRemoteControl.BotService;

internal static class WindowCache
{
    private static readonly ConcurrentDictionary<long, CacheEntry> Cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public static void Set(long userId, List<WindowInfo> items)
    {
        Cache[userId] = new CacheEntry(items, DateTimeOffset.UtcNow);
    }

    public static bool TryGet(long userId, out List<WindowInfo> items)
    {
        items = new List<WindowInfo>();
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

    private record CacheEntry(List<WindowInfo> Items, DateTimeOffset CreatedAt);
}
