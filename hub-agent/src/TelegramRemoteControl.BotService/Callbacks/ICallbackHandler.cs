namespace TelegramRemoteControl.BotService.Callbacks;

public interface ICallbackHandler
{
    string Prefix { get; }
    Task HandleAsync(CallbackContext ctx);
}
