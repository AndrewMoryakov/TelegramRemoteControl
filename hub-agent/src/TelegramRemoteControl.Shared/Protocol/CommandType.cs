namespace TelegramRemoteControl.Shared.Protocol;

public enum CommandType
{
    // Info
    Status,
    Processes,
    Drives,
    Ip,
    Monitor,
    Uptime,

    // Screen
    Screenshot,
    WindowsList,
    WindowAction,
    WindowScreenshot,

    // Shell
    Cmd,
    PowerShell,

    // Control
    Kill,
    Lock,
    Services,
    ServiceAction,
    Shutdown,
    Restart,
    Sleep,
    Hibernate,

    // Files
    FileList,
    FileDownload,
    FilePreview,

    // System
    Ping
}
