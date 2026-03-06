using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class FileUploadExecutor : ICommandExecutor
{
    public async Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        if (command.Data == null || command.Data.Length == 0)
            return Error(command, "Нет данных файла");

        var filename = command.Parameters?.GetValueOrDefault("filename") ?? "upload.bin";

        // Sanitize filename
        filename = Path.GetFileName(filename);
        if (string.IsNullOrWhiteSpace(filename))
            filename = "upload.bin";

        // Determine destination folder
        string destDir;
        if (!string.IsNullOrWhiteSpace(command.Arguments))
        {
            destDir = command.Arguments.Trim();
        }
        else
        {
            destDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");
        }

        try
        {
            Directory.CreateDirectory(destDir);

            // Ensure unique filename
            var destPath = Path.Combine(destDir, filename);
            if (File.Exists(destPath))
            {
                var nameNoExt = Path.GetFileNameWithoutExtension(filename);
                var ext = Path.GetExtension(filename);
                var i = 1;
                while (File.Exists(destPath))
                {
                    destPath = Path.Combine(destDir, $"{nameNoExt}_{i}{ext}");
                    i++;
                }
            }

            await File.WriteAllBytesAsync(destPath, command.Data, ct);

            return new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = $"✅ Файл сохранён:\n`{destPath}`"
            };
        }
        catch (Exception ex)
        {
            return Error(command, $"Ошибка сохранения файла: {ex.Message}");
        }
    }

    private static AgentResponse Error(AgentCommand command, string message) => new()
    {
        RequestId = command.RequestId,
        Type = ResponseType.Error,
        Success = false,
        ErrorMessage = message
    };
}
