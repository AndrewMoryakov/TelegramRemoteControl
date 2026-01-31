using System.Diagnostics;
using System.Text;

namespace TelegramRemoteControl.Agent.Execution;

public static class ShellHelper
{
    public static async Task<string> RunAsync(string fileName, string arguments, CancellationToken ct, Encoding? outputEncoding = null)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = outputEncoding ?? Encoding.GetEncoding(866),
            StandardErrorEncoding = outputEncoding ?? Encoding.GetEncoding(866)
        };

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var result = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(output))
            result.AppendLine(output);
        if (!string.IsNullOrWhiteSpace(error))
            result.AppendLine($"⚠️ Ошибки:\n{error}");

        return result.Length > 0 ? result.ToString() : "✅ Выполнено (нет вывода)";
    }
}
