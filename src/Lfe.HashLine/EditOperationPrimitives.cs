namespace Lfe.HashLine;

public static class EditOperationPrimitives
{
    public static string[] ApplySetLine(IReadOnlyList<string> lines, string anchor, object newText, bool skipValidation = false)
    {
        if (!skipValidation)
        {
            Validation.ValidateLineRef(lines, anchor);
        }

        var line = Validation.ParseLineRef(anchor).Line;
        var result = lines.ToList();
        var originalLine = lines[line - 1];
        var corrected = AutocorrectReplacementLines.AutocorrectReplacementLinesForEdit([originalLine], LinePayloadUtilities.ToNewLines(newText));
        var replacement = corrected.Select((entry, index) => index == 0 ? EditTextNormalization.RestoreLeadingIndent(originalLine, entry) : entry).ToArray();
        result.RemoveAt(line - 1);
        result.InsertRange(line - 1, replacement);
        return result.ToArray();
    }

    public static string[] ApplyReplaceLines(IReadOnlyList<string> lines, string startAnchor, string endAnchor, object newText, bool skipValidation = false)
    {
        if (!skipValidation)
        {
            Validation.ValidateLineRef(lines, startAnchor);
            Validation.ValidateLineRef(lines, endAnchor);
        }

        var startLine = Validation.ParseLineRef(startAnchor).Line;
        var endLine = Validation.ParseLineRef(endAnchor).Line;
        if (startLine > endLine)
        {
            throw new ArgumentException($"Invalid range: start line {startLine} cannot be greater than end line {endLine}");
        }

        var result = lines.ToList();
        var originalRange = lines.Skip(startLine - 1).Take(endLine - startLine + 1).ToArray();
        var stripped = EditTextNormalization.StripRangeBoundaryEcho(lines, startLine, endLine, LinePayloadUtilities.ToNewLines(newText));
        var corrected = AutocorrectReplacementLines.AutocorrectReplacementLinesForEdit(originalRange, stripped);
        var restored = corrected.Select((entry, index) => index == 0 ? EditTextNormalization.RestoreLeadingIndent(lines[startLine - 1], entry) : entry).ToArray();

        result.RemoveRange(startLine - 1, endLine - startLine + 1);
        result.InsertRange(startLine - 1, restored);
        return result.ToArray();
    }

    public static string[] ApplyInsertAfter(IReadOnlyList<string> lines, string anchor, object text, bool skipValidation = false)
    {
        if (!skipValidation)
        {
            Validation.ValidateLineRef(lines, anchor);
        }

        var line = Validation.ParseLineRef(anchor).Line;
        var result = lines.ToList();
        var newLines = EditTextNormalization.StripInsertAnchorEcho(lines[line - 1], LinePayloadUtilities.ToNewLines(text));
        if (newLines.Length == 0)
        {
            throw new ArgumentException($"append (anchored) requires non-empty text for {anchor}");
        }

        result.InsertRange(line, newLines);
        return result.ToArray();
    }

    public static string[] ApplyInsertBefore(IReadOnlyList<string> lines, string anchor, object text, bool skipValidation = false)
    {
        if (!skipValidation)
        {
            Validation.ValidateLineRef(lines, anchor);
        }

        var line = Validation.ParseLineRef(anchor).Line;
        var result = lines.ToList();
        var newLines = EditTextNormalization.StripInsertBeforeEcho(lines[line - 1], LinePayloadUtilities.ToNewLines(text));
        if (newLines.Length == 0)
        {
            throw new ArgumentException($"prepend (anchored) requires non-empty text for {anchor}");
        }

        result.InsertRange(line - 1, newLines);
        return result.ToArray();
    }

    public static string[] ApplyAppend(IReadOnlyList<string> lines, object text)
    {
        var normalized = LinePayloadUtilities.ToNewLines(text);
        if (normalized.Length == 0)
        {
            throw new ArgumentException("append requires non-empty text");
        }

        if (lines.Count == 1 && lines[0] == string.Empty)
        {
            return normalized;
        }

        return lines.Concat(normalized).ToArray();
    }

    public static string[] ApplyPrepend(IReadOnlyList<string> lines, object text)
    {
        var normalized = LinePayloadUtilities.ToNewLines(text);
        if (normalized.Length == 0)
        {
            throw new ArgumentException("prepend requires non-empty text");
        }

        if (lines.Count == 1 && lines[0] == string.Empty)
        {
            return normalized;
        }

        return normalized.Concat(lines).ToArray();
    }
}
