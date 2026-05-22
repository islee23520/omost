namespace Omodot.HashLine;

public static class FileTextCanonicalization
{
    public static FileTextEnvelope CanonicalizeFileText(string content)
    {
        var (withoutBom, hadBom) = StripBom(content);
        return new FileTextEnvelope(NormalizeToLf(withoutBom), hadBom, DetectLineEnding(withoutBom));
    }

    public static string RestoreFileText(string content, FileTextEnvelope envelope)
    {
        var withLineEndings = envelope.LineEnding == "\n"
            ? content
            : content.Replace("\n", "\r\n", StringComparison.Ordinal);

        return envelope.HadBom ? $"\uFEFF{withLineEndings}" : withLineEndings;
    }

    private static string DetectLineEnding(string content)
    {
        var crlfIndex = content.IndexOf("\r\n", StringComparison.Ordinal);
        var lfIndex = content.IndexOf('\n');
        if (lfIndex < 0 || crlfIndex < 0)
        {
            return "\n";
        }

        return crlfIndex < lfIndex ? "\r\n" : "\n";
    }

    private static (string Content, bool HadBom) StripBom(string content)
    {
        if (!content.StartsWith("\uFEFF", StringComparison.Ordinal))
        {
            return (content, false);
        }

        return (content[1..], true);
    }

    private static string NormalizeToLf(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
    }
}
