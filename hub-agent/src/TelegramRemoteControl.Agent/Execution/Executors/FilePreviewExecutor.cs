using System.Text;
using TelegramRemoteControl.Shared.Protocol;
using TelegramRemoteControl.Agent;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class FilePreviewExecutor : ICommandExecutor
{
    private readonly AgentSettings _settings;

    public FilePreviewExecutor(AgentSettings settings)
    {
        _settings = settings;
    }

    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var path = GetPath(command);
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(Error(command, "Путь не указан"));

        if (Directory.Exists(path))
            return Task.FromResult(Error(command, "Это папка, предпросмотр недоступен"));

        if (!File.Exists(path))
            return Task.FromResult(Error(command, "Файл не найден"));

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (LooksBinary(stream))
                return Task.FromResult(Error(command, "Бинарный файл, предпросмотр недоступен"));

            stream.Position = 0;

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var maxPreviewChars = _settings.FilePreviewMaxChars > 0 ? _settings.FilePreviewMaxChars : 4000;
            var buffer = new char[maxPreviewChars];
            var read = reader.Read(buffer, 0, buffer.Length);
            var text = read > 0 ? new string(buffer, 0, read) : string.Empty;

            return Task.FromResult(new AgentResponse
            {
                RequestId = command.RequestId,
                Type = ResponseType.Text,
                Success = true,
                Text = text
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(Error(command, $"Ошибка чтения файла: {ex.Message}"));
        }
    }

    private bool LooksBinary(Stream stream)
    {
        var probeSize = _settings.FileBinaryProbeBytes > 0 ? _settings.FileBinaryProbeBytes : 8000;
        var buffer = new byte[probeSize];
        var read = stream.Read(buffer, 0, buffer.Length);
        if (read == 0)
            return false;

        var control = 0;
        for (var i = 0; i < read; i++)
        {
            var b = buffer[i];
            if (b == 0)
                return true;

            if (b < 0x09 || (b > 0x0D && b < 0x20))
                control++;
        }

        return control > read * 0.1;
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
