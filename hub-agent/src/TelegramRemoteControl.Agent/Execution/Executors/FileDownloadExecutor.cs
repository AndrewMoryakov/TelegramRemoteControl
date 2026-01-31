using TelegramRemoteControl.Shared.Protocol;
using TelegramRemoteControl.Agent;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class FileDownloadExecutor : ICommandExecutor
{
    private readonly AgentSettings _settings;

    public FileDownloadExecutor(AgentSettings settings)
    {
        _settings = settings;
    }

    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var path = GetPath(command);
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(Error(command, "Путь не указан"));

        if (Directory.Exists(path))
            return Task.FromResult(Error(command, "Это папка, скачивание недоступно"));

        if (!File.Exists(path))
            return Task.FromResult(Error(command, "Файл не найден"));

        FileInfo info;
        try
        {
            info = new FileInfo(path);
        }
        catch (Exception ex)
        {
            return Task.FromResult(Error(command, $"Ошибка чтения файла: {ex.Message}"));
        }

        var maxDownload = _settings.FileMaxDownloadBytes > 0
            ? _settings.FileMaxDownloadBytes
            : 45L * 1024 * 1024;

        if (info.Length > maxDownload)
            return Task.FromResult(Error(command, "Файл слишком большой для отправки"));

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Document,
                Success = true,
                Data = buffer.ToArray(),
                FileName = info.Name
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(Error(command, $"Ошибка чтения файла: {ex.Message}"));
        }
    }

    private static string? GetPath(AgentCommand command)
    {
        if (command.Parameters != null &&
            command.Parameters.TryGetValue("path", out var path) &&
            !string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return null;
    }

    private static AgentResponse Error(AgentCommand command, string message)
    {
        return new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Error,
            Success = false,
            ErrorMessage = message
        };
    }
}
