using System.Collections.Concurrent;
using TelegramRemoteControl.BotService.Models;

namespace TelegramRemoteControl.BotService;

// BL-12: keyed by (userId, agentId) — see ProcessCache for rationale.
internal static class ServiceCache
{
    private static readonly ConcurrentDictionary<(long UserId, string AgentId), CacheEntry> Cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public static void Set(long userId, string? agentId, List<ServiceInfo> items)
    {
        if (string.IsNullOrEmpty(agentId)) return;
        Cache[(userId, agentId)] = new CacheEntry(items, DateTimeOffset.UtcNow);
    }

    public static bool TryGet(long userId, string? agentId, out List<ServiceInfo> items)
    {
        items = new List<ServiceInfo>();
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

    private record CacheEntry(List<ServiceInfo> Items, DateTimeOffset CreatedAt);
}
