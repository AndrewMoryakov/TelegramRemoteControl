namespace TelegramRemoteControl.BotService;

internal static class ShellAccess
{
    public static bool IsAllowed(BotSettings settings, long userId)
    {
        if (settings.ShellAllowedUsers.Length > 0)
            return settings.ShellAllowedUsers.Contains(userId);

        // Fallback: shell is restricted to admin users when no explicit allowlist is configured.
        return settings.AuthorizedUsers.Contains(userId);
    }

    public static bool TryValidateArgument(BotSettings settings, string? arguments, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrEmpty(arguments))
            return true;

        var limit = settings.ShellMaxArgumentLength > 0 ? settings.ShellMaxArgumentLength : 2000;
        if (arguments.Length > limit)
        {
            errorMessage = $"❌ Команда слишком длинная ({arguments.Length} > {limit} символов)";
            return false;
        }

        return true;
    }
}
