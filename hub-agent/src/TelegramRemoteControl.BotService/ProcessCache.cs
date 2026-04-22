using System.Collections.Concurrent;
using TelegramRemoteControl.BotService.Models;

namespace TelegramRemoteControl.BotService;

// BL-12: keyed by (userId, agentId) so pressing a kill button produced on PC1 does
// not resolve against PC2's PID table after the user switches devices. A null/empty
// agentId acts as a miss — the caller should re-run /processes.
internal static class ProcessCache
{
    private static readonly ConcurrentDictionary<(long UserId, string AgentId), CacheEntry> Cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public static void Set(long userId, string? agentId, List<ProcessInfo> items)
    {
        if (string.IsNullOrEmpty(agentId)) return;
        Cache[(userId, agentId)] = new CacheEntry(items, DateTimeOffset.UtcNow);
    }

    public static bool TryGet(long userId, string? agentId, out List<ProcessInfo> items)
    {
        items = new List<ProcessInfo>();
        if (string.IsNullOrEmpty(agentId)) return false;

        var key = (userId, agentId);
        if (!Cache.TryGetValue(key, out var entry))
            return false;

        if (DateTimeOffset.UtcNow - entry.CreatedAt > Ttl)
        {
            Cache.TryRemove(key, out _);
            return false;
        }

        items = entry.Items;
        return true;
    }

    private record CacheEntry(List<ProcessInfo> Items, DateTimeOffset CreatedAt);
}
