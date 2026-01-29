namespace TelegramRemoteControl.Commands;

/// <summary>
/// Маркер для команд, требующих подтверждения
/// </summary>
public interface IConfirmableCommand
{
    /// <summary>Сообщение для подтверждения</summary>
    string ConfirmMessage { get; }
}
