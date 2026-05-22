namespace Omodot.Utils;

public sealed record JsoncParseError(string Message, int Offset, int Length);

public sealed record JsoncParseResult<T>(T? Data, IReadOnlyList<JsoncParseError> Errors);

public sealed record DetectPluginConfigResult(string Format, string Path, string? LegacyPath = null);

public sealed record DetectPluginConfigFileOptions(IReadOnlyList<string> Basenames, IReadOnlyList<string>? LegacyBasenames = null)
{
    public DetectPluginConfigFileOptions(IReadOnlyList<string> basenames) : this(basenames, null)
    {
    }
}
