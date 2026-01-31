namespace TelegramRemoteControl.BotService.Commands;

public interface ICommand
{
    string Id { get; }
    string[] Aliases { get; }
    string Title { get; }
    string? Icon { get; }
    string? Description { get; }
    string Category { get; }
    Task ExecuteAsync(CommandContext ctx);
}
