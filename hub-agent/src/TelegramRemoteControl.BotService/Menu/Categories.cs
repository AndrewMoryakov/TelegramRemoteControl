namespace TelegramRemoteControl.BotService.Menu;

/// <summary>
/// Категории команд для меню
/// </summary>
public static class Categories
{
    public const string Info = "info";
    public const string Screen = "screen";
    public const string Shell = "shell";
    public const string Control = "control";
    public const string System = "system";

    public static string GetTitle(string category) => category switch
    {
        Info => "📊 Информация",
        Screen => "🖥 Экран",
        Shell => "⚡ Команды",
        Control => "🔧 Управление",
        System => "⚙️ Система",
        _ => category
    };

    /// <summary>Порядок категорий в меню</summary>
    public static readonly string[] Order = { Info, Screen, Shell, Control, System };
}
