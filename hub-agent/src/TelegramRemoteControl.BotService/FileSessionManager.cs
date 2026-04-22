using System.Collections.Concurrent;
using TelegramRemoteControl.BotService.Models;

namespace TelegramRemoteControl.BotService;

internal static class FileSessionManager
{
    private static readonly ConcurrentDictionary<long, FileSession> Sessions = new();

    public static FileSession Get(long userId)
    {
        return Sessions.GetOrAdd(userId, _ => new FileSession());
    }

    internal class FileSession
    {
        private readonly Dictionary<int, string> _pathCache = new();
        private int _nextId;

        // BL-11: callers serialize handler invocations on this semaphore so two parallel
        // taps (navigate + page, for example) don't mutate _pathCache / Items concurrently.
        public SemaphoreSlim Lock { get; } = new(1, 1);

        public string? CurrentPath { get; private set; }
        public int CurrentPage { get; set; }
        public List<FileItem> Items { get; private set; } = new();
        public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.MinValue;

        public void Set(string? path, List<FileItem> items)
        {
            CurrentPath = path;
            Items = items;
            CurrentPage = 0;
            UpdatedAt = DateTimeOffset.UtcNow;
            _pathCache.Clear();
            _nextId = 0;
        }

        public bool IsStale(TimeSpan ttl) => DateTimeOffset.UtcNow - UpdatedAt > ttl;

        public int CachePath(string path)
        {
            var id = _nextId++;
            _pathCache[id] = path;
            return id;
        }

        public bool TryGetPath(int id, out string path)
        {
            return _pathCache.TryGetValue(id, out path!);
        }
    }
}
