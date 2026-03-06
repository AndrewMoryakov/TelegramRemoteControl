namespace TelegramRemoteControl.BotService.Commands;

public class CommandRegistry
{
    private readonly Dictionary<string, ICommand> _byAlias = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ICommand> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ICommand> _commands = new();

    public CommandRegistry(IEnumerable<ICommand> commands)
    {
        Console.Error.WriteLine("[DIAG] CommandRegistry ctor: start");
        foreach (var cmd in commands)
        {
            Console.Error.WriteLine("[DIAG] CommandRegistry: registering " + cmd.Id);
            _commands.Add(cmd);
            _byId[cmd.Id] = cmd;
            foreach (var alias in cmd.Aliases)
            {
                _byAlias[alias] = cmd;
            }
        }
        Console.Error.WriteLine("[DIAG] CommandRegistry ctor: done, count=" + _commands.Count);
    }

    public ICommand? FindByAlias(string alias)
    {
        _byAlias.TryGetValue(alias, out var cmd);
        return cmd;
    }

    public ICommand? FindById(string id)
    {
        _byId.TryGetValue(id, out var cmd);
        return cmd;
    }

    public IEnumerable<ICommand> GetByCategory(string category)
    {
        return _commands.Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<ICommand> GetAll() => _commands;
}
