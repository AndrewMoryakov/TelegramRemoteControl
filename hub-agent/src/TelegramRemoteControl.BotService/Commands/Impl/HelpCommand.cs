using TelegramRemoteControl.BotService.Menu;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class HelpCommand : ICommand
{
    private readonly IEnumerable<ICommand> _commands;

    public HelpCommand(IEnumerable<ICommand> commands)
    {
        _commands = commands;
    }

    public string Id => "help";
    public string[] Aliases => new[] { "/help" };
    public string Title => "Помощь";
    public string? Icon => "❓";
    public string? Description => "Список команд";
    public string Category => Categories.System;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var lines = new List<string> { "*Доступные команды:*", "" };

        foreach (var category in Categories.Order)
        {
            var commands = _commands
                .Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase) && c.Id != Id)
                .ToList();
            if (commands.Count == 0) continue;

            lines.Add($"*{Categories.GetTitle(category)}*");
            foreach (var cmd in commands)
            {
                var alias = cmd.Aliases.FirstOrDefault() ?? $"/{cmd.Id}";
                var desc = string.IsNullOrWhiteSpace(cmd.Description) ? cmd.Title : cmd.Description;
                lines.Add($"  {cmd.Icon} `{alias}` — {desc}");
            }
            lines.Add("");
        }

        await ctx.ReplyWithBack(string.Join('\n', lines));
    }
}
