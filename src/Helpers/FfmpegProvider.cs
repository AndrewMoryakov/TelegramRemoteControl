using System.IO.Compression;

namespace TelegramRemoteControl.Helpers;

/// <summary>
/// Finds or auto-downloads ffmpeg.exe.
/// Cached in {AppData}/TelegramRemoteControl/ffmpeg/ffmpeg.exe
/// </summary>
public static class FfmpegProvider
{
    private const string DownloadUrl =
        "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

    private const string ZipEntryPath = "ffmpeg-master-latest-win64-gpl/bin/ffmpeg.exe";

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TelegramRemoteControl", "ffmpeg");

    private static readonly string CachedExe = Path.Combine(CacheDir, "ffmpeg.exe");

    private static readonly SemaphoreSlim _downloadLock = new(1, 1);

    /// <summary>
    /// Returns true if ffmpeg.exe is already available (PATH, common locations, or cached).
    /// Does NOT trigger a download.
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
            foreach (var dir in pathDirs)
            {
                try
                {
                    if (File.Exists(Path.Combine(dir, "ffmpeg.exe"))) return true;
                }
                catch { }
            }

            if (File.Exists(@"C:\ffmpeg\bin\ffmpeg.exe")) return true;
            if (File.Exists(@"C:\Program Files\ffmpeg\bin\ffmpeg.exe")) return true;
            if (File.Exists(@"C:\tools\ffmpeg\bin\ffmpeg.exe")) return true;
            if (File.Exists(CachedExe)) return true;

            return false;
        }
    }

    /// <summary>
    /// Returns the path to ffmpeg.exe — from PATH, common locations, or auto-downloaded cache.
    /// Returns null only if download fails.
    /// <paramref name="onProgress"/> is called periodically during download with (bytesDownloaded, totalBytes).
    /// totalBytes may be null if the server does not provide Content-Length.
    /// </summary>
    public static async Task<string?> GetFfmpegPathAsync(
        CancellationToken ct = default,
        Func<long, long?, Task>? onProgress = null)
    {
        // 1. Check PATH
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
        foreach (var dir in pathDirs)
        {
            try
            {
                var candidate = Path.Combine(dir, "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }

        // 2. Common locations
        var commonPaths = new[]
        {
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\tools\ffmpeg\bin\ffmpeg.exe"
        };
        foreach (var p in commonPaths)
        {
            if (File.Exists(p)) return p;
        }

        // 3. Cached download
        if (File.Exists(CachedExe)) return CachedExe;

        // 4. Download
        return await DownloadAsync(ct, onProgress);
    }

    private static async Task<string?> DownloadAsync(
        CancellationToken ct,
        Func<long, long?, Task>? onProgress)
    {
        await _downloadLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (File.Exists(CachedExe)) return CachedExe;

            Directory.CreateDirectory(CacheDir);

            var zipPath = Path.Combine(CacheDir, "ffmpeg.zip");

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                using var response = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;

                await using var fs = File.Create(zipPath);
                await using var stream = await response.Content.ReadAsStreamAsync(ct);

                var buffer = new byte[81920];
                long downloaded = 0;
                var lastReport = DateTimeOffset.MinValue;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    downloaded += bytesRead;

                    if (onProgress != null)
                    {
                        var now = DateTimeOffset.UtcNow;
                        if (now - lastReport >= TimeSpan.FromSeconds(3))
                        {
                            lastReport = now;
                            await onProgress(downloaded, totalBytes);
                        }
                    }
                }

                // Final progress report
                if (onProgress != null)
                    await onProgress(downloaded, totalBytes);
            }
            catch
            {
                // Clean up partial download
                if (File.Exists(zipPath)) File.Delete(zipPath);
                return null;
            }

            try
            {
                using var zip = ZipFile.OpenRead(zipPath);
                var entry = zip.GetEntry(ZipEntryPath);
                if (entry == null) return null;

                entry.ExtractToFile(CachedExe, overwrite: true);
                return CachedExe;
            }
            catch
            {
                if (File.Exists(CachedExe)) File.Delete(CachedExe);
                return null;
            }
            finally
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
            }
        }
        finally
        {
            _downloadLock.Release();
        }
    }
}
