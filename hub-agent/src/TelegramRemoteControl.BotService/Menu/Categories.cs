namespace TelegramRemoteControl.BotService.Menu;

/// <summary>
/// Категории команд для меню
/// </summary>
public static class Categories
{
    public const string Ai = "ai";
    public const string Info = "info";
    public const string Screen = "screen";
    public const string Shell = "shell";
    public const string Control = "control";
    public const string System = "system";
    public const string Admin = "admin";

    public static string GetTitle(string category) => category switch
    {
        Ai => "🧠 AI",
        Info => "📊 Информация",
        Screen => "🖥 Экран",
        Shell => "⚡ Команды",
        Control => "🔧 Управление",
        System => "⚙️ Система",
        Admin => "🔐 Админ",
        _ => category
    };

    /// <summary>Порядок категорий в меню</summary>
    public static readonly string[] Order = { Ai, Info, Screen, Shell, Control, System, Admin };
}
