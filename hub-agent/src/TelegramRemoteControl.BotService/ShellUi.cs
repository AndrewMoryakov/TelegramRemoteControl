using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramRemoteControl.BotService;

internal static class ShellUi
{
    public static InlineKeyboardMarkup ModeKeyboard(string label) =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData($"🛑 Выйти из {label}", "shell:exit") }
        });

    public static string WrapCode(string text) =>
        text.Contains("```", StringComparison.Ordinal) ? text : $"```\n{text}\n```";
}
