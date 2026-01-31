using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TelegramRemoteControl.Agent.Helpers;

public static class ScreenshotHelper
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    public static void CaptureAndSave(string filePath)
    {
        SetProcessDPIAware();

        var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        bitmap.Save(filePath, ImageFormat.Png);
    }
}
