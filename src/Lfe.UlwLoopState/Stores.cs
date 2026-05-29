namespace Lfe.UlwLoopState;

public sealed class MemoryUlwLoopStateStore : IUlwLoopStateStore
{
    private UlwLoopState? _state;
    public MemoryUlwLoopStateStore(UlwLoopState? initialState = null) => _state = initialState;
    public UlwLoopState? Read() => _state;
    public void Write(UlwLoopState state) => _state = state with { };
    public void Clear() => _state = null;
}

public sealed class FileUlwLoopStateStore : IUlwLoopStateStore
{
    private readonly string _filePath;
    private readonly string _basePath;

    public FileUlwLoopStateStore(string directory, string? customPath = null)
    {
        _basePath = Path.GetFullPath(directory);
        _filePath = GetStateFilePath(directory, customPath);
    }

    public UlwLoopState? Read()
    {
        if (!File.Exists(_filePath)) return null;
        try
        {
            var content = File.ReadAllText(_filePath);
            return StateSerializer.Deserialize(content);
        }
        catch { return null; }
    }

    public void Write(UlwLoopState state)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, StateSerializer.Serialize(state));
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
            try { File.Delete(_filePath); } catch { }
    }

    public static string GetStateFilePath(string directory, string? customPath = null)
    {
        var basePath = Path.GetFullPath(directory);
        var statePath = Path.GetFullPath(Path.Join(basePath, customPath ?? UlwLoopConstants.DefaultStateFile));
        var rel = Path.GetRelativePath(basePath, statePath);
        if (rel.StartsWith("..") || Path.IsPathRooted(rel))
            throw new ArgumentException("ULW loop state path must stay inside the base directory");
        return statePath;
    }
}
