using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramRemoteControl.Commands.Impl;
using TelegramRemoteControl.Menu;
using File = System.IO.File;

namespace TelegramRemoteControl.Callbacks.Impl;

/// <summary>
/// Обработчик callback для управления окнами (win:*)
/// </summary>
public class WindowCallbackHandler : ICallbackHandler
{
    public string Prefix => "win";

    private readonly MenuBuilder _menu;

    public WindowCallbackHandler(MenuBuilder menu)
    {
        _menu = menu;
    }

    public async Task HandleAsync(CallbackContext ctx)
    {
        if (ctx.Args.Length == 0)
        {
            await ctx.AnswerAsync("❌ Неверный формат");
            return;
        }

        var action = ctx.Args[0];

        switch (action)
        {
            case "list":
                await ListAsync(ctx);
                break;
            case "info":
                await InfoAsync(ctx);
                break;
            case "ss":
                await ScreenshotAsync(ctx);
                break;
            case "min":
            case "max":
            case "restore":
            case "close":
                await ActionAsync(ctx, action);
                break;
            case "minall":
                await MinimizeAllAsync(ctx);
                break;
            case "closeall":
                await CloseAllAsync(ctx);
                break;
            case "closeall_confirm":
                await CloseAllConfirmAsync(ctx);
                break;
            default:
                await ctx.AnswerAsync("❓ Неизвестное действие");
                break;
        }
    }

    private async Task ListAsync(CallbackContext ctx)
    {
        await ctx.AnswerAsync("🔄 Обновляю...");

        try
        {
            var windows = await WindowsCommand.EnumWindowsAsync(ctx.CancellationToken);
            var cache = WindowsCommand.GetCached(ctx.ChatId);
            // Update cache via reflection-free approach: just re-enumerate and store
            // We need the static cache — call EnumWindowsAsync which doesn't cache,
            // so we access the cache field indirectly through the command.
            // Actually, let's just set it directly since the field is accessible.

            if (windows.Count == 0)
            {
                await ctx.EditTextAsync("🪟 Нет открытых окон", _menu.BackButton());
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("🪟 *Окна*\n");

            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                var stateIcon = w.State switch
                {
                    "minimized" => "➖",
                    "maximized" => "➕",
                    _ => "🔲"
                };
                var title = w.Title.Length > 40 ? w.Title[..37] + "..." : w.Title;
                sb.AppendLine($"`{i + 1}.` {stateIcon} {EscapeMarkdown(title)}");
            }

            sb.AppendLine($"\n_Всего: {windows.Count}_");

            var rows = new List<InlineKeyboardButton[]>();
            for (int i = 0; i < Math.Min(windows.Count, 10); i++)
            {
                var w = windows[i];
                var label = w.Title.Length > 20 ? w.Title[..17] + "..." : w.Title;
                rows.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"{i + 1}. {label}", $"win:info:{w.Hwnd}")
                });
            }

            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("➖ Свернуть все", "win:minall"),
                InlineKeyboardButton.WithCallbackData("❌ Закрыть все", "win:closeall")
            });
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 Обновить", "win:list"),
                InlineKeyboardButton.WithCallbackData("◀️ Меню", "menu")
            });

            await ctx.EditTextAsync(sb.ToString(), new InlineKeyboardMarkup(rows));
        }
        catch (Exception ex)
        {
            await ctx.EditTextAsync($"❌ Ошибка:\n```\n{ex.Message}\n```", _menu.BackButton());
        }
    }

    private async Task InfoAsync(CallbackContext ctx)
    {
        if (ctx.Args.Length < 2 || !long.TryParse(ctx.Args[1], out long hwnd))
        {
            await ctx.AnswerAsync("❌ Неверный формат");
            return;
        }

        // Find window in cache or re-enumerate
        var windows = await WindowsCommand.EnumWindowsAsync(ctx.CancellationToken);
        var win = windows.FirstOrDefault(w => w.Hwnd == hwnd);

        if (win == null)
        {
            await ctx.AnswerAsync("❌ Окно не найдено", showAlert: true);
            return;
        }

        var stateIcon = win.State switch
        {
            "minimized" => "➖",
            "maximized" => "➕",
            _ => "🔲"
        };

        var text = $"""
            🪟 *Окно*

            📝 Заголовок: `{EscapeMarkdown(win.Title)}`
            🔢 HWND: `{win.Hwnd}`
            🔢 PID: `{win.Pid}`
            {stateIcon} Состояние: `{win.State}`
            """;

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📸 Скриншот", $"win:ss:{hwnd}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("➖ Свернуть", $"win:min:{hwnd}"),
                InlineKeyboardButton.WithCallbackData("➕ Развернуть", $"win:max:{hwnd}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 Восстановить", $"win:restore:{hwnd}"),
                InlineKeyboardButton.WithCallbackData("❌ Закрыть", $"win:close:{hwnd}")
            },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ К списку", "win:list") }
        });

        await ctx.EditTextAsync(text, keyboard);
        await ctx.AnswerAsync();
    }

    private async Task ScreenshotAsync(CallbackContext ctx)
    {
        if (ctx.Args.Length < 2 || !long.TryParse(ctx.Args[1], out long hwnd))
        {
            await ctx.AnswerAsync("❌ Неверный формат");
            return;
        }

        await ctx.AnswerAsync("📸 Делаю скриншот окна...");

        string? tempFile = null;
        try
        {
            tempFile = WindowsCommand.CaptureWindow(hwnd);
            await using var stream = File.OpenRead(tempFile);

            // Send screenshot as photo with a "back to info" keyboard
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("◀️ К окну", $"win:info:{hwnd}") },
                new[] { InlineKeyboardButton.WithCallbackData("◀️ К списку", "win:list") }
            });

            await ctx.Bot.SendPhoto(ctx.ChatId,
                InputFile.FromStream(stream, "window.png"),
                replyMarkup: keyboard,
                cancellationToken: ctx.CancellationToken);
        }
        catch (Exception ex)
        {
            await ctx.Bot.SendMessage(ctx.ChatId,
                $"❌ Ошибка скриншота окна:\n```\n{ex.Message}\n```",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                cancellationToken: ctx.CancellationToken);
        }
        finally
        {
            if (tempFile != null && File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private async Task ActionAsync(CallbackContext ctx, string action)
    {
        if (ctx.Args.Length < 2 || !long.TryParse(ctx.Args[1], out long hwnd))
        {
            await ctx.AnswerAsync("❌ Неверный формат");
            return;
        }

        var actionName = action switch
        {
            "min" => "Свёрнуто",
            "max" => "Развёрнуто",
            "restore" => "Восстановлено",
            "close" => "Закрыто",
            _ => action
        };

        var actionIcon = action switch
        {
            "min" => "➖",
            "max" => "➕",
            "restore" => "🔄",
            "close" => "❌",
            _ => "✅"
        };

        try
        {
            WindowsCommand.RunWindowAction(action, hwnd);
            await ctx.AnswerAsync($"{actionIcon} {actionName}", showAlert: true);

            // After close, go back to list; otherwise refresh info
            if (action == "close")
                await ListAsync(ctx);
            else
                await InfoAsync(ctx);
        }
        catch (Exception ex)
        {
            await ctx.AnswerAsync($"❌ {ex.Message}", showAlert: true);
        }
    }

    private async Task MinimizeAllAsync(CallbackContext ctx)
    {
        try
        {
            WindowsCommand.RunMinimizeAll();
            await ctx.AnswerAsync("➖ Все окна свёрнуты", showAlert: true);
        }
        catch (Exception ex)
        {
            await ctx.AnswerAsync($"❌ {ex.Message}", showAlert: true);
        }
    }

    private async Task CloseAllAsync(CallbackContext ctx)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Да, закрыть все", "win:closeall_confirm"),
                InlineKeyboardButton.WithCallbackData("❌ Нет", "win:list")
            }
        });

        await ctx.EditTextAsync("⚠️ *Закрыть все окна?*\n\nЭто действие закроет все видимые окна.", keyboard);
        await ctx.AnswerAsync();
    }

    private async Task CloseAllConfirmAsync(CallbackContext ctx)
    {
        try
        {
            var windows = await WindowsCommand.EnumWindowsAsync(ctx.CancellationToken);
            int closed = 0;

            foreach (var win in windows)
            {
                try
                {
                    WindowsCommand.RunWindowAction("close", win.Hwnd);
                    closed++;
                }
                catch { /* skip windows that can't be closed */ }
            }

            await ctx.AnswerAsync($"❌ Закрыто окон: {closed}", showAlert: true);
            await ListAsync(ctx);
        }
        catch (Exception ex)
        {
            await ctx.AnswerAsync($"❌ {ex.Message}", showAlert: true);
        }
    }

    private static string EscapeMarkdown(string text) =>
        text.Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`").Replace("[", "\\[");
}
