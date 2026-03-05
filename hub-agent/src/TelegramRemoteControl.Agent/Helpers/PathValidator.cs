namespace TelegramRemoteControl.Agent.Helpers;

public static class PathValidator
{
    /// <summary>
    /// Нормализует путь (разрешает ../, symlinks) и проверяет, что он находится внутри rootPath (если задан).
    /// Возвращает null если путь невалиден или выходит за пределы разрешённого корня.
    /// </summary>
    public static string? Normalize(string path, string? rootPath = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (path.Contains('\0'))
            return null;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(rootPath))
        {
            string normalizedRoot;
            try
            {
                normalizedRoot = Path.GetFullPath(rootPath);
            }
            catch
            {
                return null;
            }

            if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return null;
        }

        return fullPath;
    }
}
