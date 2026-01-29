using System.Diagnostics;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.Menu;

namespace TelegramRemoteControl.Callbacks.Impl;

/// <summary>
/// Обработчик callback для управления процессами (proc:*)
/// </summary>
public class ProcessCallbackHandler : ICallbackHandler
{
    public string Prefix => "proc";

    private readonly MenuBuilder _menu;

    public ProcessCallbackHandler(MenuBuilder menu)
    {
        _menu = menu;
    }

    public async Task HandleAsync(CallbackContext ctx)
    {
        if (ctx.Args.Length < 2)
        {
            await ctx.AnswerAsync("❌ Неверный формат");
            return;
        }

        var action = ctx.Args[0];

        var result = action switch
        {
            "kill" => await KillAsync(ctx),
            "info" => await InfoAsync(ctx),
            "pri" => await SetPriorityAsync(ctx),
            _ => false
        };

        if (!result)
            await ctx.AnswerAsync("❓ Неизвестное действие");
    }

    private async Task<bool> KillAsync(CallbackContext ctx)
    {
        if (!int.TryParse(ctx.Args[1], out int pid))
            return false;

        try
        {
            var proc = Process.GetProcessById(pid);
            var name = proc.ProcessName;
            proc.Kill();

            await ctx.AnswerAsync($"💀 {name} завершён", showAlert: true);
            await ctx.EditTextAsync($"💀 Процесс `{name}` (PID: {pid}) завершён", _menu.BackButton());
        }
        catch (Exception ex)
        {
            await ctx.AnswerAsync($"❌ {ex.Message}", showAlert: true);
        }

        return true;
    }

    private async Task<bool> InfoAsync(CallbackContext ctx)
    {
        if (!int.TryParse(ctx.Args[1], out int pid))
            return false;

        try
        {
            var proc = Process.GetProcessById(pid);

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("💀 Завершить", $"proc:kill:{pid}"),
                    InlineKeyboardButton.WithCallbackData("🔄 Обновить", $"proc:info:{pid}")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬆️", $"proc:pri:high:{pid}"),
                    InlineKeyboardButton.WithCallbackData("➡️", $"proc:pri:normal:{pid}"),
                    InlineKeyboardButton.WithCallbackData("⬇️", $"proc:pri:low:{pid}")
                },
                new[] { InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu") }
            });

            var text = $"""
                📋 *Процесс*

                📝 Имя: `{proc.ProcessName}`
                🔢 PID: `{proc.Id}`
                💾 RAM: `{proc.WorkingSet64 / 1024 / 1024} MB`
                🧵 Потоков: `{proc.Threads.Count}`
                ⏱ Запущен: `{proc.StartTime:g}`
                ⚙️ Приоритет: `{proc.PriorityClass}`
                """;

            await ctx.EditTextAsync(text, keyboard);
            await ctx.AnswerAsync();
        }
        catch (Exception ex)
        {
            await ctx.AnswerAsync($"❌ {ex.Message}", showAlert: true);
        }

        return true;
    }

    private async Task<bool> SetPriorityAsync(CallbackContext ctx)
    {
        // proc:pri:level:pid -> Args = ["pri", "level", "pid"]
        if (ctx.Args.Length < 3 || !int.TryParse(ctx.Args[2], out int pid))
            return false;

        var level = ctx.Args[1];

        try
        {
            var proc = Process.GetProcessById(pid);
            proc.PriorityClass = level switch
            {
                "high" => ProcessPriorityClass.High,
                "low" => ProcessPriorityClass.BelowNormal,
                _ => ProcessPriorityClass.Normal
            };

            var icon = level switch { "high" => "⬆️", "low" => "⬇️", _ => "➡️" };
            await ctx.AnswerAsync($"{icon} Приоритет: {proc.PriorityClass}");
        }
        catch (Exception ex)
        {
            await ctx.AnswerAsync($"❌ {ex.Message}", showAlert: true);
        }

        return true;
    }
}
