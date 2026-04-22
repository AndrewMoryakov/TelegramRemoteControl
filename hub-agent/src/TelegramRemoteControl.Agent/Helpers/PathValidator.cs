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

            var rootTrimmed = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rootWithSep = rootTrimmed + Path.DirectorySeparatorChar;

            var matchesRootItself = fullPath.Equals(rootTrimmed, StringComparison.OrdinalIgnoreCase);
            var matchesChild = fullPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase);
            if (!matchesRootItself && !matchesChild)
                return null;
        }

        return fullPath;
    }
}
