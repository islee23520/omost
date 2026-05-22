namespace Omodot.AstGrep;

public static class ResultFormatter
{
    public static string FormatSearchResult(SgResult result)
    {
        if (!string.IsNullOrEmpty(result.Error))
        {
            return $"Error: {result.Error}";
        }

        if (result.Matches.Count == 0)
        {
            return "No matches found";
        }

        var lines = new List<string>();

        if (result.Truncated)
        {
            lines.Add($"[TRUNCATED] Results truncated ({DescribeTruncation(result)})\n");
        }

        lines.Add($"Found {result.Matches.Count} match(es){(result.Truncated ? $" (truncated from {result.TotalMatches})" : string.Empty)}:\n");

        foreach (var match in result.Matches)
        {
            var loc = $"{match.File}:{match.Range.Start.Line + 1}:{match.Range.Start.Column + 1}";
            lines.Add(loc);
            lines.Add($"  {match.Lines.Trim()}");
            lines.Add(string.Empty);
        }

        return string.Join("\n", lines);
    }

    public static string FormatReplaceResult(SgResult result, bool isDryRun)
    {
        if (!string.IsNullOrEmpty(result.Error))
        {
            return $"Error: {result.Error}";
        }

        if (result.Matches.Count == 0)
        {
            return "No matches found to replace";
        }

        var prefix = isDryRun ? "[DRY RUN] " : string.Empty;
        var lines = new List<string>();

        if (result.Truncated)
        {
            lines.Add($"[TRUNCATED] Results truncated ({DescribeTruncation(result)})\n");
        }

        lines.Add($"{prefix}{result.Matches.Count} replacement(s):\n");

        foreach (var match in result.Matches)
        {
            var loc = $"{match.File}:{match.Range.Start.Line + 1}:{match.Range.Start.Column + 1}";
            lines.Add(loc);
            lines.Add($"  {match.Text}");
            lines.Add(string.Empty);
        }

        if (isDryRun)
        {
            lines.Add("Use dryRun=false to apply changes");
        }

        return string.Join("\n", lines);
    }

    private static string DescribeTruncation(SgResult result)
    {
        return result.TruncatedReason switch
        {
            SgTruncatedReason.MaxMatches => $"showing first {result.Matches.Count} of {result.TotalMatches}",
            SgTruncatedReason.MaxOutputBytes => "output exceeded 1MB limit",
            _ => "search timed out",
        };
    }
}
