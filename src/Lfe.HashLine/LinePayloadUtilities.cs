namespace Lfe.HashLine;

internal static class LinePayloadUtilities
{
    public static string[] ToNewLines(object payload)
    {
        return payload switch
        {
            string text => EditTextNormalization.ToNewLines(text),
            string[] lines => EditTextNormalization.ToNewLines(lines),
            IReadOnlyList<string> lines => EditTextNormalization.ToNewLines(lines),
            IEnumerable<string> lines => EditTextNormalization.ToNewLines(lines.ToArray()),
            _ => throw new ArgumentException("Payload must be a string or a string collection.", nameof(payload)),
        };
    }
}
