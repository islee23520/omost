using System.Text.Json;

namespace Omodot.AstGrep;

public static class SgCompactJsonOutput
{
    public static SgResult CreateSgResultFromStdout(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return new SgResult { Matches = Array.Empty<CliMatch>(), TotalMatches = 0, Truncated = false };
        }

        var outputTruncated = stdout.Length >= CliLanguages.DefaultMaxOutputBytes;
        var outputToProcess = outputTruncated ? stdout[..CliLanguages.DefaultMaxOutputBytes] : stdout;

        List<CliMatch> matches;
        try
        {
            matches = DeserializeMatches(outputToProcess);
        }
        catch (JsonException)
        {
            if (!outputTruncated)
            {
                return new SgResult { Matches = Array.Empty<CliMatch>(), TotalMatches = 0, Truncated = false };
            }

            var searchIndex = outputToProcess.Length;
            while (searchIndex > 0)
            {
                var bracketIndex = outputToProcess.LastIndexOf("},", searchIndex - 1, StringComparison.Ordinal);
                if (bracketIndex <= 0)
                {
                    break;
                }

                try
                {
                    var truncatedJson = outputToProcess[..(bracketIndex + 1)] + "]";
                    matches = DeserializeMatches(truncatedJson);
                    return BuildResult(matches, outputTruncated);
                }
                catch (JsonException)
                {
                    searchIndex = bracketIndex;
                }
            }

            return new SgResult
            {
                Matches = Array.Empty<CliMatch>(),
                TotalMatches = 0,
                Truncated = true,
                TruncatedReason = SgTruncatedReason.MaxOutputBytes,
                Error = "Output too large and could not be parsed",
            };
        }

        return BuildResult(matches, outputTruncated);
    }

    private static List<CliMatch> DeserializeMatches(string json)
    {
        return JsonSerializer.Deserialize<List<CliMatch>>(json) ?? [];
    }

    private static SgResult BuildResult(List<CliMatch> matches, bool outputTruncated)
    {
        var totalMatches = matches.Count;
        var matchesTruncated = totalMatches > CliLanguages.DefaultMaxMatches;
        IReadOnlyList<CliMatch> finalMatches = matchesTruncated
            ? matches.Take(CliLanguages.DefaultMaxMatches).ToArray()
            : matches;

        return new SgResult
        {
            Matches = finalMatches,
            TotalMatches = totalMatches,
            Truncated = outputTruncated || matchesTruncated,
            TruncatedReason = outputTruncated
                ? SgTruncatedReason.MaxOutputBytes
                : matchesTruncated
                    ? SgTruncatedReason.MaxMatches
                    : null,
        };
    }
}
