namespace TelegramRemoteControl.Helpers;

public enum FileCategory
{
    Image,
    Video,
    Audio,
    Archive,
    Document,
    Code,
    Text,
    Executable,
    Other
}

public record FileTypeInfo(string Icon, FileCategory Category, bool IsPreviewable, bool IsTextReadable);

public static class FileTypeRegistry
{
    private static readonly Dictionary<string, FileTypeInfo> _types = new(StringComparer.OrdinalIgnoreCase);

    static FileTypeRegistry()
    {
        Register("🖼️", FileCategory.Image, previewable: true, textReadable: false,
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".ico");
        Register("🎬", FileCategory.Video, previewable: true, textReadable: false,
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".webm");
        Register("🎵", FileCategory.Audio, previewable: false, textReadable: false,
            ".mp3", ".wav", ".flac", ".ogg", ".aac");
        Register("📦", FileCategory.Archive, previewable: false, textReadable: false,
            ".zip", ".rar", ".7z", ".tar", ".gz");
        Register("📋", FileCategory.Document, previewable: false, textReadable: false,
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx");
        Register("💻", FileCategory.Code, previewable: false, textReadable: true,
            ".cs", ".js", ".ts", ".py", ".json", ".xml", ".html", ".css",
            ".jsx", ".tsx", ".vue", ".svelte", ".go", ".rs", ".java", ".kt",
            ".cpp", ".c", ".h", ".hpp", ".rb", ".php", ".swift", ".scala",
            ".sql", ".graphql", ".proto", ".yaml", ".yml", ".toml",
            ".dockerfile", ".makefile", ".cmake", ".gradle", ".sln", ".csproj",
            ".shtml", ".xhtml", ".sass", ".scss", ".less");
        Register("📝", FileCategory.Text, previewable: false, textReadable: true,
            ".txt", ".md", ".log", ".ini", ".cfg", ".conf", ".config",
            ".env", ".properties", ".editorconfig", ".gitignore", ".gitattributes",
            ".dockerignore", ".htaccess", ".csv", ".tsv", ".rst", ".tex");
        Register("⚙️", FileCategory.Executable, previewable: false, textReadable: false,
            ".exe", ".msi");
        Register("📜", FileCategory.Code, previewable: false, textReadable: true,
            ".bat", ".cmd", ".ps1", ".sh", ".bash", ".zsh", ".fish", ".psm1", ".psd1");
    }

    private static void Register(string icon, FileCategory category, bool previewable, bool textReadable, params string[] extensions)
    {
        var info = new FileTypeInfo(icon, category, previewable, textReadable);
        foreach (var ext in extensions)
            _types[ext] = info;
    }

    public static FileTypeInfo GetInfo(string extension)
    {
        return _types.TryGetValue(extension, out var info)
            ? info
            : new FileTypeInfo("📄", FileCategory.Other, false, false);
    }
}
