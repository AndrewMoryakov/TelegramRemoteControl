namespace TelegramRemoteControl.Commands.Impl;

public class DrivesCommand : CommandBase
{
    public override string Id => "drives";
    public override string[] Aliases => new[] { "/drives", "/disks" };
    public override string Title => "Диски";
    public override string Icon => "💾";
    public override string Description => "Информация о дисках";
    public override string Category => Categories.Info;

    public override async Task ExecuteAsync(CommandContext ctx)
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => $"{d.Name} {d.DriveType}: {d.AvailableFreeSpace / 1024 / 1024 / 1024} GB свободно из {d.TotalSize / 1024 / 1024 / 1024} GB");

        var list = string.Join("\n", drives);
        await ctx.ReplyWithBack($"💾 *Диски:*\n```\n{list}\n```");
    }
}
