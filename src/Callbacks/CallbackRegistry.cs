namespace TelegramRemoteControl.Callbacks;

/// <summary>
/// Реестр обработчиков callback
/// </summary>
public class CallbackRegistry
{
    private readonly Dictionary<string, ICallbackHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public CallbackRegistry Register(ICallbackHandler handler)
    {
        _handlers[handler.Prefix.TrimEnd(':')] = handler;
        return this;
    }

    public CallbackRegistry Register(params ICallbackHandler[] handlers)
    {
        foreach (var h in handlers) Register(h);
        return this;
    }

    /// <summary>Найти обработчик по данным callback</summary>
    public (ICallbackHandler? Handler, string[] Args) Match(string data)
    {
        var colonIndex = data.IndexOf(':');
        if (colonIndex < 0)
            return (null, Array.Empty<string>());

        var prefix = data[..colonIndex];
        var rest = data[(colonIndex + 1)..];
        var args = rest.Split(':');

        return _handlers.TryGetValue(prefix, out var handler)
            ? (handler, args)
            : (null, Array.Empty<string>());
    }
}
