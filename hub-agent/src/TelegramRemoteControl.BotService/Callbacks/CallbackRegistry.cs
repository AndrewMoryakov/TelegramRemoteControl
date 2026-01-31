namespace TelegramRemoteControl.BotService.Callbacks;

public class CallbackRegistry
{
    private readonly List<ICallbackHandler> _handlers;

    public CallbackRegistry(IEnumerable<ICallbackHandler> handlers)
    {
        _handlers = handlers.ToList();
    }

    public ICallbackHandler? FindByData(string callbackData)
    {
        return _handlers.FirstOrDefault(h => callbackData.StartsWith(h.Prefix + ":"));
    }
}
