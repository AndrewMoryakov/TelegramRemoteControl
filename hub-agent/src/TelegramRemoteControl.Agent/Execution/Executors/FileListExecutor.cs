using System.Text.Json;
using TelegramRemoteControl.Agent.Helpers;
using TelegramRemoteControl.Shared.Protocol;

namespace TelegramRemoteControl.Agent.Execution.Executors;

public class FileListExecutor : ICommandExecutor
{
    private readonly string? _rootPath;

    public FileListExecutor(AgentSettings settings)
    {
        _rootPath = string.IsNullOrWhiteSpace(settings.FileRootPath) ? null : settings.FileRootPath;
    }

    public Task<AgentResponse> ExecuteAsync(AgentCommand command, CancellationToken ct = default)
    {
        var rawPath = GetRawPath(command);

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            if (OperatingSystem.IsWindows() && string.IsNullOrWhiteSpace(_rootPath))
                return Task.FromResult(ListDrives(command));

            rawPath = _rootPath ?? Path.GetPathRoot(Environment.CurrentDirectory) ?? "/";
        }

        var path = PathValidator.Normalize(rawPath, _rootPath);
        if (path == null)
            return Task.FromResult(Error(command, "Недопустимый путь"));

        return Task.FromResult(ListDirectory(command, path));
    }

    private static string? GetRawPath(AgentCommand command)
    {
        if (command.Parameters != null &&
            command.Parameters.TryGetValue("path", out var path) &&
            !string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return null;
    }

    private static AgentResponse ListDrives(AgentCommand command)
    {
        var items = new List<FileEntry>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                    continue;

                items.Add(new FileEntry
                {
                    Name = drive.Name,
                    IsDirectory = true,
                    Free = drive.AvailableFreeSpace,
                    Total = drive.TotalSize
                });
            }
            catch
            {
                // skip drives we cannot read
            }
        }

        var payload = new FileListPayload
        {
            Path = null,
            Items = items
        };

        return new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Structured,
            Success = true,
            JsonPayload = JsonSerializer.Serialize(payload)
        };
    }

    private static AgentResponse ListDirectory(AgentCommand command, string path)
    {
        if (!Directory.Exists(path))
            return Error(command, "Папка не найдена");

        var items = new List<FileEntry>();

        try
        {
            var info = new DirectoryInfo(path);

            foreach (var dir in info.EnumerateDirectories())
            {
                try
                {
                    items.Add(new FileEntry
                    {
                        Name = dir.Name,
                        IsDirectory = true,
                        Modified = dir.LastWriteTime
                    });
                }
                catch
                {
                    // ignore directories we cannot read
                }
            }

            foreach (var file in info.EnumerateFiles())
            {
                try
                {
                    items.Add(new FileEntry
                    {
                        Name = file.Name,
                        IsDirectory = false,
                        Size = file.Length,
                        Modified = file.LastWriteTime
                    });
                }
                catch
                {
                    // ignore files we cannot read
                }
            }
        }
        catch (Exception ex)
        {
            return Error(command, $"Ошибка чтения папки: {ex.Message}");
        }

        var payload = new FileListPayload
        {
            Path = path,
            Items = items
        };

        return new AgentResponse
        {
            RequestId = command.RequestId,
            Type = ResponseType.Structured,
            Success = true,
            JsonPayload = JsonSerializer.Serialize(payload)
        };
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

    private sealed class FileListPayload
    {
        public string? Path { get; set; }
        public List<FileEntry> Items { get; set; } = new();
    }

    private sealed class FileEntry
    {
        public string Name { get; set; } = "";
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime? Modified { get; set; }
        public long? Free { get; set; }
        public long? Total { get; set; }
    }
}
