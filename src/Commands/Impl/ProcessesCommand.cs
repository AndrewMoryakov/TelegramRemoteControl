using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramRemoteControl.Commands.Impl;

public class ProcessesCommand : CommandBase
{
    public override string Id => "processes";
    public override string[] Aliases => new[] { "/processes", "/proc" };
    public override string Title => "Процессы";
    public override string Icon => "📋";
    public override string Description => "Список процессов";
    public override string Category => Categories.Info;

    // Кэш процессов для выбора по номеру
    private static readonly Dictionary<long, List<ProcessInfo>> _cache = new();

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        // Если передан номер — показать меню управления
        if (!string.IsNullOrWhiteSpace(ctx.Arguments) && int.TryParse(ctx.Arguments, out int num))
        {
            await ShowProcessMenu(ctx, num);
            return;
        }

        await SendAsync(ctx, "⏳ Сбор информации о процессах...");

        var processes = GetTopProcesses();
        _cache[ctx.ChatId] = processes;

        var lines = processes.Select((p, i) =>
            string.Format("{0,2}. {1,-22} {2,5} MB  {3,5:F1}%",
                i + 1, Truncate(p.Name, 22), p.RamMb, p.CpuPercent));

        var text = $"📋 *Топ-20 процессов:*\n```\n №  Имя                      RAM    CPU\n{string.Join("\n", lines)}\n```\n\n💡 Выбрать: `/proc <номер>`";

        await ctx.ReplyWithBack(text);
    }

    private async Task ShowProcessMenu(CommandContext ctx, int number)
    {
        if (!_cache.TryGetValue(ctx.ChatId, out var processes) || number < 1 || number > processes.Count)
        {
            await ctx.ReplyWithBack("❌ Сначала выполните `/proc` для получения списка");
            return;
        }

        var proc = processes[number - 1];

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("💀 Завершить", $"proc:kill:{proc.Pid}"),
                InlineKeyboardButton.WithCallbackData("🔄 Обновить", $"proc:info:{proc.Pid}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⬆️ Высокий", $"proc:pri:high:{proc.Pid}"),
                InlineKeyboardButton.WithCallbackData("➡️ Обычный", $"proc:pri:normal:{proc.Pid}"),
                InlineKeyboardButton.WithCallbackData("⬇️ Низкий", $"proc:pri:low:{proc.Pid}")
            },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "processes") }
        });

        var text = $"""
            📋 *Процесс #{number}*

            📝 Имя: `{proc.Name}`
            🔢 PID: `{proc.Pid}`
            💾 RAM: `{proc.RamMb} MB`
            ⚡ CPU: `{proc.CpuPercent:F1}%`
            """;

        await ctx.Bot.SendMessage(ctx.ChatId, text,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            replyMarkup: keyboard,
            cancellationToken: ctx.CancellationToken);
    }

    private List<ProcessInfo> GetTopProcesses()
    {
        var result = new List<ProcessInfo>();

        foreach (var proc in Process.GetProcesses().OrderByDescending(p => p.WorkingSet64).Take(20))
        {
            try
            {
                double cpu = 0;
                try
                {
                    var startTime = DateTime.UtcNow;
                    var startCpu = proc.TotalProcessorTime;
                    Thread.Sleep(100); // Короткий замер
                    var endCpu = proc.TotalProcessorTime;
                    var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    cpu = (endCpu - startCpu).TotalMilliseconds / elapsed / Environment.ProcessorCount * 100;
                }
                catch { }

                result.Add(new ProcessInfo
                {
                    Pid = proc.Id,
                    Name = proc.ProcessName,
                    RamMb = (int)(proc.WorkingSet64 / 1024 / 1024),
                    CpuPercent = cpu
                });
            }
            catch { }
        }

        return result;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 2)] + "..";

    public class ProcessInfo
    {
        public int Pid { get; set; }
        public string Name { get; set; } = "";
        public int RamMb { get; set; }
        public double CpuPercent { get; set; }
    }
}
