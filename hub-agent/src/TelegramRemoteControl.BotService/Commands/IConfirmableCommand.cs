namespace TelegramRemoteControl.BotService.Commands;

public interface IConfirmableCommand : ICommand
{
    string ConfirmMessage { get; }
    Task ExecuteConfirmedAsync(CommandContext ctx);
}
