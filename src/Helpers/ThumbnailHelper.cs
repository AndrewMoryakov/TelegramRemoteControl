using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

namespace TelegramRemoteControl.Helpers;

public static class ThumbnailHelper
{
    public static async Task<bool> TryGenerateThumbnailAsync(string filePath, string outputPath,
        int maxSize = 320, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(filePath);
        var info = FileTypeRegistry.GetInfo(ext);

        try
        {
            return info.Category switch
            {
                FileCategory.Image => GenerateImageThumbnail(filePath, outputPath, maxSize),
                FileCategory.Video => await GenerateVideoThumbnailAsync(filePath, outputPath, maxSize, ct),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private static bool GenerateImageThumbnail(string filePath, string outputPath, int maxSize)
    {
        using var original = Image.FromFile(filePath);

        var ratio = Math.Min((double)maxSize / original.Width, (double)maxSize / original.Height);
        if (ratio >= 1.0)
        {
            // Image is already small enough, just copy as JPEG
            original.Save(outputPath, ImageFormat.Jpeg);
            return true;
        }

        var newWidth = (int)(original.Width * ratio);
        var newHeight = (int)(original.Height * ratio);

        using var thumb = new Bitmap(newWidth, newHeight);
        using var g = Graphics.FromImage(thumb);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(original, 0, 0, newWidth, newHeight);
        thumb.Save(outputPath, ImageFormat.Jpeg);
        return true;
    }

    private static async Task<bool> GenerateVideoThumbnailAsync(string filePath, string outputPath,
        int maxSize, CancellationToken ct)
    {
        var ffmpeg = await FfmpegProvider.GetFfmpegPathAsync(ct);
        if (ffmpeg == null) return false;

        var args = string.Format(
            "-ss 1 -i \"{0}\" -frames:v 1 -vf \"scale={1}:{1}:force_original_aspect_ratio=decrease\" -y \"{2}\"",
            filePath, maxSize, outputPath);

        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        if (process == null) return false;

        await process.WaitForExitAsync(ct);
        return process.ExitCode == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
    }
}
