using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Omodot.HashLine;

public static class Validation
{
    private const int MismatchContext = 2;
    private static readonly Regex LineRefExtractPattern = new(
        @"([0-9]+#[ZPMQVRWSNKTXJBYH]{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NormalizeLineRef(string reference)
    {
        var originalTrimmed = reference.Trim();
        var trimmed = originalTrimmed;
        trimmed = Regex.Replace(trimmed, @"^(?:>>>|[+-])\s*", string.Empty, RegexOptions.CultureInvariant);
        trimmed = Regex.Replace(trimmed, @"\s*#\s*", "#", RegexOptions.CultureInvariant);
        trimmed = Regex.Replace(trimmed, @"\|.*$", string.Empty, RegexOptions.CultureInvariant);
        trimmed = trimmed.Trim();

        if (HashlineConstants.HashlineRefPattern.IsMatch(trimmed))
        {
            return trimmed;
        }

        var extracted = LineRefExtractPattern.Match(trimmed);
        return extracted.Success ? extracted.Groups[1].Value : originalTrimmed;
    }

    public static LineRef ParseLineRef(string reference)
    {
        var normalized = NormalizeLineRef(reference);
        var match = HashlineConstants.HashlineRefPattern.Match(normalized);
        if (match.Success)
        {
            return new LineRef(int.Parse(match.Groups[1].Value), match.Groups[2].Value);
        }

        var hashIndex = normalized.IndexOf('#');
        if (hashIndex > 0)
        {
            var prefix = normalized[..hashIndex];
            var suffix = normalized[(hashIndex + 1)..];
            if (!prefix.All(char.IsDigit) && Regex.IsMatch(suffix, "^[ZPMQVRWSNKTXJBYH]{2}$", RegexOptions.CultureInvariant))
            {
                throw new ArgumentException($"Invalid line reference: \"{reference}\". \"{prefix}\" is not a line number. Use the actual line number from the read output.");
            }
        }

        throw new ArgumentException($"Invalid line reference format: \"{reference}\". Expected format: \"{{line_number}}#{{hash_id}}\"");
    }

    public static void ValidateLineRef(IReadOnlyList<string> lines, string reference)
    {
        var parsed = ParseLineRefWithHint(reference, lines);
        if (parsed.Line < 1 || parsed.Line > lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(reference), $"Line number {parsed.Line} out of bounds. File has {lines.Count} lines.");
        }

        var content = lines[parsed.Line - 1];
        if (!IsCompatibleLineHash(parsed.Line, content, parsed.Hash))
        {
            throw new HashlineMismatchError(new[] { (parsed.Line, parsed.Hash) }, lines);
        }
    }

    public static void ValidateLineRefs(IReadOnlyList<string> lines, IReadOnlyList<string> references)
    {
        var mismatches = new List<(int Line, string Expected)>();
        foreach (var reference in references)
        {
            var parsed = ParseLineRefWithHint(reference, lines);
            if (parsed.Line < 1 || parsed.Line > lines.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(references), $"Line number {parsed.Line} out of bounds (file has {lines.Count} lines)");
            }

            var content = lines[parsed.Line - 1];
            if (!IsCompatibleLineHash(parsed.Line, content, parsed.Hash))
            {
                mismatches.Add((parsed.Line, parsed.Hash));
            }
        }

        if (mismatches.Count > 0)
        {
            throw new HashlineMismatchError(mismatches, lines);
        }
    }

    private static bool IsCompatibleLineHash(int line, string content, string hash)
    {
        return HashComputation.ComputeLineHash(line, content) == hash || HashComputation.ComputeLegacyLineHash(line, content) == hash;
    }

    private static string? SuggestLineForHash(string reference, IReadOnlyList<string> lines)
    {
        var match = Regex.Match(reference.Trim(), @"#([ZPMQVRWSNKTXJBYH]{2})$", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        var hash = match.Groups[1].Value;
        for (var index = 0; index < lines.Count; index += 1)
        {
            if (IsCompatibleLineHash(index + 1, lines[index], hash))
            {
                return $"Did you mean \"{index + 1}#{HashComputation.ComputeLineHash(index + 1, lines[index])}\"?";
            }
        }

        return null;
    }

    private static LineRef ParseLineRefWithHint(string reference, IReadOnlyList<string> lines)
    {
        try
        {
            return ParseLineRef(reference);
        }
        catch (Exception error)
        {
            var hint = SuggestLineForHash(reference, lines);
            throw hint is null ? error : new ArgumentException($"{error.Message} {hint}", error);
        }
    }

    public sealed class HashlineMismatchError : Exception
    {
        public IReadOnlyDictionary<string, string> Remaps { get; }

        public HashlineMismatchError(IReadOnlyList<(int Line, string Expected)> mismatches, IReadOnlyList<string> fileLines)
            : base(FormatMessage(mismatches, fileLines))
        {
            var remaps = new Dictionary<string, string>();
            foreach (var mismatch in mismatches)
            {
                var actual = HashComputation.ComputeLineHash(mismatch.Line, fileLines[mismatch.Line - 1]);
                remaps[$"{mismatch.Line}#{mismatch.Expected}"] = $"{mismatch.Line}#{actual}";
            }

            Remaps = new ReadOnlyDictionary<string, string>(remaps);
        }

        public static string FormatMessage(IReadOnlyList<(int Line, string Expected)> mismatches, IReadOnlyList<string> fileLines)
        {
            var mismatchByLine = mismatches.ToDictionary(item => item.Line, item => item.Expected);
            var displayLines = new SortedSet<int>();
            foreach (var mismatch in mismatches)
            {
                var low = Math.Max(1, mismatch.Line - MismatchContext);
                var high = Math.Min(fileLines.Count, mismatch.Line + MismatchContext);
                for (var line = low; line <= high; line += 1)
                {
                    displayLines.Add(line);
                }
            }

            var output = new List<string>
            {
                $"{mismatches.Count} line{(mismatches.Count > 1 ? "s have" : " has")} changed since last read. Use updated {{line_number}}#{{hash_id}} references below (>>> marks changed lines).",
                string.Empty,
            };

            var previousLine = -1;
            foreach (var line in displayLines)
            {
                if (previousLine != -1 && line > previousLine + 1)
                {
                    output.Add("    ...");
                }

                previousLine = line;
                var content = fileLines[line - 1];
                var hash = HashComputation.ComputeLineHash(line, content);
                var prefix = $"{line}#{hash}|{content}";
                output.Add(mismatchByLine.ContainsKey(line) ? $">>> {prefix}" : $"    {prefix}");
            }

            return string.Join("\n", output);
        }
    }
}
