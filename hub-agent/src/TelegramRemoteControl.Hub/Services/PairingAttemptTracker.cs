using System.Collections.Concurrent;

namespace TelegramRemoteControl.Hub.Services;

/// <summary>
/// Rate-limits failed RegisterAgent attempts per remote IP to mitigate pairing-code brute force.
/// </summary>
public class PairingAttemptTracker
{
    private readonly ConcurrentDictionary<string, Window> _windows = new();

    public bool IsBlocked(string remoteKey, int maxFailuresPerMinute)
    {
        if (maxFailuresPerMinute <= 0)
            return false;

        if (!_windows.TryGetValue(remoteKey, out var window))
            return false;

        lock (window)
        {
            RollIfNeeded(window);
            return window.Count >= maxFailuresPerMinute;
        }
    }

    public void RegisterFailure(string remoteKey)
    {
        var window = _windows.GetOrAdd(remoteKey, _ => new Window());
        lock (window)
        {
            RollIfNeeded(window);
            window.Count++;
        }
    }

    public void Clear(string remoteKey)
    {
        _windows.TryRemove(remoteKey, out _);
    }

    private static void RollIfNeeded(Window window)
    {
        var now = DateTime.UtcNow;
        if (now - window.WindowStart > TimeSpan.FromMinutes(1))
        {
            window.WindowStart = now;
            window.Count = 0;
        }
    }

    private sealed class Window
    {
        public DateTime WindowStart { get; set; } = DateTime.UtcNow;
        public int Count { get; set; }
    }
}
