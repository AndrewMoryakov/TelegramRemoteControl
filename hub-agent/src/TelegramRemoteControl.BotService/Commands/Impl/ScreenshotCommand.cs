using TelegramRemoteControl.BotService.Menu;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.BotService.Commands.Impl;

public class ScreenshotCommand : ProxyCommandBase
{
    public override string Id => "screenshot";
    public override string[] Aliases => new[] { "/screenshot", "/ss", "/screen" };
    public override string Title => "Скриншот";
    public override string? Icon => "📸";
    public override string? Description => "Скриншот экрана";
    public override string Category => Categories.Screen;

    protected override CommandType AgentCommandType => CommandType.Screenshot;
}
