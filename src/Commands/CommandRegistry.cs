namespace TelegramRemoteControl.Commands;

/// <summary>
/// Реестр всех команд бота
/// </summary>
public class CommandRegistry
{
    private readonly Dictionary<string, ICommand> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ICommand> _byAlias = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ICommand> _commands = new();

    /// <summary>Все зарегистрированные команды</summary>
    public IReadOnlyList<ICommand> All => _commands;

    /// <summary>Команды по категориям</summary>
    public ILookup<string, ICommand> ByCategory => _commands.ToLookup(c => c.Category);

    /// <summary>Регистрация команды</summary>
    public CommandRegistry Register(ICommand command)
    {
        _commands.Add(command);
        _byId[command.Id] = command;

        foreach (var alias in command.Aliases)
        {
            _byAlias[alias.TrimStart('/')] = command;
        }

        return this;
    }

    /// <summary>Регистрация нескольких команд</summary>
    public CommandRegistry Register(params ICommand[] commands)
    {
        foreach (var cmd in commands)
            Register(cmd);
        return this;
    }

    /// <summary>Найти команду по ID (для callback)</summary>
    public ICommand? FindById(string id) =>
        _byId.TryGetValue(id, out var cmd) ? cmd : null;

    /// <summary>Найти команду по текстовому алиасу</summary>
    public ICommand? FindByAlias(string alias) =>
        _byAlias.TryGetValue(alias.TrimStart('/'), out var cmd) ? cmd : null;

    /// <summary>Получить команды категории</summary>
    public IEnumerable<ICommand> GetByCategory(string category) =>
        _commands.Where(c => c.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
}
