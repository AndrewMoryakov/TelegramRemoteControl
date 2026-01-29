using System.Diagnostics;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace TelegramRemoteControl.Commands;

/// <summary>
/// Базовый класс команды с общими утилитами
/// </summary>
public abstract class CommandBase : ICommand
{
    public abstract string Id { get; }
    public abstract string[] Aliases { get; }
    public abstract string Title { get; }
    public abstract string Icon { get; }
    public abstract string Description { get; }
    public abstract string Category { get; }

    public abstract Task ExecuteAsync(CommandContext context);

    /// <summary>Отправить текстовое сообщение</summary>
    protected async Task SendAsync(CommandContext ctx, string text, ParseMode parseMode = ParseMode.Markdown)
    {
        await ctx.Bot.SendMessage(ctx.ChatId, text, parseMode: parseMode, cancellationToken: ctx.CancellationToken);
    }

    /// <summary>Выполнить shell-команду и вернуть результат</summary>
    protected async Task<string> RunShellAsync(string fileName, string arguments, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.GetEncoding(866),
            StandardErrorEncoding = Encoding.GetEncoding(866)
        };

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var result = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(output))
            result.AppendLine(output);
        if (!string.IsNullOrWhiteSpace(error))
            result.AppendLine($"⚠️ Ошибки:\n{error}");

        return result.Length > 0 ? result.ToString() : "✅ Выполнено (нет вывода)";
    }

    /// <summary>Отправить длинное сообщение с разбивкой</summary>
    protected async Task SendLongAsync(CommandContext ctx, string text)
    {
        const int maxLength = 4000;

        if (text.Length <= maxLength)
        {
            await SendAsync(ctx, $"```\n{text}\n```");
            return;
        }

        for (int i = 0; i < text.Length; i += maxLength)
        {
            var chunk = text.Substring(i, Math.Min(maxLength, text.Length - i));
            await SendAsync(ctx, $"```\n{chunk}\n```");
        }
    }
}
