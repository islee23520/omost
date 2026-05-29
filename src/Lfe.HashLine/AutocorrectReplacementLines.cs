using System.Text;
using System.Text.RegularExpressions;

namespace Lfe.HashLine;

public static class AutocorrectReplacementLines
{
    private static readonly Regex TrailingContinuationRegex = new(
        @"(?:&&|\|\||\?\?|\?|:|=|,|\+|-|\*|\/|\.|\()\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MergeOperatorRegex = new(
        @"[|&?]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NormalizeTokens(string text)
    {
        return StripAllWhitespace(text);
    }

    public static string StripAllWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    public static string StripTrailingContinuationTokens(string text)
    {
        return TrailingContinuationRegex.Replace(text, string.Empty);
    }

    public static string StripMergeOperatorChars(string text)
    {
        return MergeOperatorRegex.Replace(text, string.Empty);
    }

    public static string[] RestoreOldWrappedLines(IReadOnlyList<string> originalLines, IReadOnlyList<string> replacementLines)
    {
        if (originalLines.Count == 0 || replacementLines.Count < 2)
        {
            return replacementLines.ToArray();
        }

        var canonicalToOriginal = new Dictionary<string, (string Line, int Count)>();
        foreach (var line in originalLines)
        {
            var canonical = StripAllWhitespace(line);
            if (canonicalToOriginal.TryGetValue(canonical, out var existing))
            {
                canonicalToOriginal[canonical] = (existing.Line, existing.Count + 1);
            }
            else
            {
                canonicalToOriginal[canonical] = (line, 1);
            }
        }

        var candidates = new List<(int Start, int Length, string Replacement, string Canonical)>();
        for (var start = 0; start < replacementLines.Count; start += 1)
        {
            for (var length = 2; length <= 10 && start + length <= replacementLines.Count; length += 1)
            {
                var span = replacementLines.Skip(start).Take(length).ToArray();
                if (span.Any(line => line.Trim().Length == 0))
                {
                    continue;
                }

                var canonicalSpan = StripAllWhitespace(string.Concat(span));
                if (canonicalToOriginal.TryGetValue(canonicalSpan, out var original) && original.Count == 1 && canonicalSpan.Length >= 6)
                {
                    candidates.Add((start, length, original.Line, canonicalSpan));
                }
            }
        }

        if (candidates.Count == 0)
        {
            return replacementLines.ToArray();
        }

        var canonicalCounts = candidates.GroupBy(candidate => candidate.Canonical).ToDictionary(group => group.Key, group => group.Count());
        var uniqueCandidates = candidates.Where(candidate => canonicalCounts[candidate.Canonical] == 1).OrderByDescending(candidate => candidate.Start).ToArray();
        if (uniqueCandidates.Length == 0)
        {
            return replacementLines.ToArray();
        }

        var corrected = replacementLines.ToList();
        foreach (var candidate in uniqueCandidates)
        {
            corrected.RemoveRange(candidate.Start, candidate.Length);
            corrected.Insert(candidate.Start, candidate.Replacement);
        }

        return corrected.ToArray();
    }

    public static string[] MaybeExpandSingleLineMerge(IReadOnlyList<string> originalLines, IReadOnlyList<string> replacementLines)
    {
        if (replacementLines.Count != 1 || originalLines.Count <= 1)
        {
            return replacementLines.ToArray();
        }

        var merged = replacementLines[0];
        var parts = originalLines.Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        if (parts.Length != originalLines.Count)
        {
            return replacementLines.ToArray();
        }

        var indices = new List<int>();
        var offset = 0;
        var orderedMatch = true;

        foreach (var part in parts)
        {
            var index = merged.IndexOf(part, offset, StringComparison.Ordinal);
            var matchedLength = part.Length;
            if (index < 0)
            {
                var stripped = StripTrailingContinuationTokens(part);
                if (stripped != part)
                {
                    index = merged.IndexOf(stripped, offset, StringComparison.Ordinal);
                    if (index >= 0)
                    {
                        matchedLength = stripped.Length;
                    }
                }
            }

            if (index < 0)
            {
                var segment = merged[offset..];
                var segmentStripped = StripMergeOperatorChars(segment);
                var partStripped = StripMergeOperatorChars(part);
                var fuzzyIndex = segmentStripped.IndexOf(partStripped, StringComparison.Ordinal);
                if (fuzzyIndex >= 0)
                {
                    var strippedPosition = 0;
                    var originalPosition = 0;
                    while (strippedPosition < fuzzyIndex && originalPosition < segment.Length)
                    {
                        if (!"|&?".Contains(segment[originalPosition], StringComparison.Ordinal))
                        {
                            strippedPosition += 1;
                        }

                        originalPosition += 1;
                    }

                    index = offset + originalPosition;
                    matchedLength = part.Length;
                }
            }

            if (index < 0)
            {
                orderedMatch = false;
                break;
            }

            indices.Add(index);
            offset = index + matchedLength;
        }

        var expanded = new List<string>();
        if (orderedMatch)
        {
            for (var index = 0; index < indices.Count; index += 1)
            {
                var start = indices[index];
                var end = index + 1 < indices.Count ? indices[index + 1] : merged.Length;
                expanded.Add(merged[start..end].Trim());
            }
        }

        if (orderedMatch && expanded.Count == originalLines.Count)
        {
            return expanded.ToArray();
        }

        var semicolonSplit = merged
            .Split(new[] { "; " }, StringSplitOptions.None)
            .Select((line, index) => index < merged.Split(new[] { "; " }, StringSplitOptions.None).Length - 1 && !line.EndsWith(';') ? $"{line};" : line)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        return semicolonSplit.Length == originalLines.Count ? semicolonSplit : replacementLines.ToArray();
    }

    public static string[] RestoreIndentForPairedReplacement(IReadOnlyList<string> originalLines, IReadOnlyList<string> replacementLines)
    {
        if (originalLines.Count != replacementLines.Count)
        {
            return replacementLines.ToArray();
        }

        return replacementLines.Select((line, index) =>
        {
            if (line.Length == 0 || LeadingWhitespace(line).Length > 0)
            {
                return line;
            }

            var indent = LeadingWhitespace(originalLines[index]);
            if (indent.Length == 0 || originalLines[index].Trim() == line.Trim())
            {
                return line;
            }

            return indent + line;
        }).ToArray();
    }

    public static string[] AutocorrectReplacement(IReadOnlyList<string> originalLines, IReadOnlyList<string> replacementLines)
    {
        var next = replacementLines.ToArray();
        next = MaybeExpandSingleLineMerge(originalLines, next);
        next = RestoreOldWrappedLines(originalLines, next);
        next = RestoreIndentForPairedReplacement(originalLines, next);
        return next;
    }

    public static string[] AutocorrectReplacementLinesForEdit(IReadOnlyList<string> originalLines, IReadOnlyList<string> replacementLines)
    {
        return AutocorrectReplacement(originalLines, replacementLines);
    }

    private static string LeadingWhitespace(string text)
    {
        var builder = new StringBuilder();
        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character))
            {
                break;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
