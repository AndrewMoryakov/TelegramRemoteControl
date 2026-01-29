using System.Drawing;
using System.Drawing.Imaging;

namespace TelegramRemoteControl.Helpers;

/// <summary>
/// Captures the primary screen and saves it to a file.
/// Used both from the --screenshot CLI path and the interactive command.
/// </summary>
public static class ScreenshotHelper
{
    public static void CaptureAndSave(string filePath)
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen
            ?? throw new InvalidOperationException("No primary screen available (headless or disconnected session)");

        var bounds = screen.Bounds;
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        bitmap.Save(filePath, ImageFormat.Png);
    }
}
