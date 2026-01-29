using Telegram.Bot;
using Message = Telegram.Bot.Types.Message;
using CallbackQuery = Telegram.Bot.Types.CallbackQuery;

namespace TelegramRemoteControl.Commands;

/// <summary>
/// Базовый интерфейс команды
/// </summary>
public interface ICommand
{
    /// <summary>Уникальный идентификатор команды (для callback)</summary>
    string Id { get; }

    /// <summary>Текстовые команды (/cmd, /ps и т.д.)</summary>
    string[] Aliases { get; }

    /// <summary>Название для отображения в меню</summary>
    string Title { get; }

    /// <summary>Эмодзи для меню</summary>
    string Icon { get; }

    /// <summary>Описание команды</summary>
    string Description { get; }

    /// <summary>Категория для группировки в меню</summary>
    string Category { get; }

    /// <summary>Выполнение команды</summary>
    Task ExecuteAsync(CommandContext context);
}

/// <summary>
/// Контекст выполнения команды
/// </summary>
public class CommandContext
{
    public ITelegramBotClient Bot { get; init; } = null!;
    public long ChatId { get; init; }
    public long UserId { get; init; }
    public string? Arguments { get; init; }
    public Message? Message { get; init; }
    public CallbackQuery? CallbackQuery { get; init; }
    public CancellationToken CancellationToken { get; init; }

    /// <summary>Отправить ответ с главным меню</summary>
    public Func<string, Task> ReplyWithMenu { get; init; } = null!;

    /// <summary>Отправить ответ с кнопкой "Назад"</summary>
    public Func<string, Task> ReplyWithBack { get; init; } = null!;
}
