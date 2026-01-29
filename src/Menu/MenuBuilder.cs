using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.Commands;

namespace TelegramRemoteControl.Menu;

/// <summary>
/// Построитель меню
/// </summary>
public class MenuBuilder
{
    private readonly CommandRegistry _registry;

    public MenuBuilder(CommandRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>Главное меню</summary>
    public InlineKeyboardMarkup MainMenu()
    {
        var rows = new List<InlineKeyboardButton[]>();

        foreach (var category in Categories.Order)
        {
            var commands = _registry.GetByCategory(category).ToList();
            if (commands.Count == 0) continue;

            // Если команд мало — показываем их напрямую
            if (commands.Count <= 2)
            {
                rows.Add(commands
                    .Select(c => InlineKeyboardButton.WithCallbackData($"{c.Icon} {c.Title}", c.Id))
                    .ToArray());
            }
            else
            {
                // Иначе — кнопка категории
                rows.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(Categories.GetTitle(category), $"cat:{category}")
                });
            }
        }

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Меню категории</summary>
    public InlineKeyboardMarkup CategoryMenu(string category)
    {
        var commands = _registry.GetByCategory(category).ToList();
        var rows = new List<InlineKeyboardButton[]>();

        // По 2 кнопки в ряд
        for (int i = 0; i < commands.Count; i += 2)
        {
            var row = commands
                .Skip(i)
                .Take(2)
                .Select(c => InlineKeyboardButton.WithCallbackData($"{c.Icon} {c.Title}", c.Id))
                .ToArray();
            rows.Add(row);
        }

        // Кнопка назад
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "menu") });

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Кнопка "Назад в меню"</summary>
    public InlineKeyboardMarkup BackButton() =>
        new(new[] { InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu") });

    /// <summary>Кнопка подтверждения опасной операции</summary>
    public InlineKeyboardMarkup ConfirmMenu(string actionId) =>
        new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Да", $"confirm:{actionId}"),
                InlineKeyboardButton.WithCallbackData("❌ Нет", "menu")
            }
        });
}
