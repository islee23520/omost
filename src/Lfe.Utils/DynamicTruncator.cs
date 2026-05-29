namespace Lfe.Utils;

public sealed record TruncationResult(string Result, bool Truncated, int? RemovedCount = null);

public sealed record TruncationOptions(int? TargetMaxTokens = null, int? PreserveHeaderLines = null, int? ContextWindowLimit = null);

public static class DynamicTruncator
{
    public const int CharsPerTokenEstimate = 4;

    public static int EstimateTokens(string text) => (int)Math.Ceiling(text.Length / (double)CharsPerTokenEstimate);

    public static TruncationResult TruncateToTokenLimit(string? output, int maxTokens, int preserveHeaderLines = 3)
    {
        output ??= string.Empty;
        var currentTokens = EstimateTokens(output);
        if (currentTokens <= maxTokens)
        {
            return new TruncationResult(output, false);
        }

        var lines = output.Split('\n');
        if (lines.Length <= preserveHeaderLines)
        {
            var maxChars = maxTokens * CharsPerTokenEstimate;
            return new TruncationResult($"{output[..Math.Min(output.Length, maxChars)]}\n\n[Output truncated due to context window limit]", true);
        }

        var headerLines = lines.Take(preserveHeaderLines).ToArray();
        var contentLines = lines.Skip(preserveHeaderLines).ToArray();
        var headerText = string.Join('\n', headerLines);
        var headerTokens = EstimateTokens(headerText);
        const int truncationMessageTokens = 50;
        var availableTokens = maxTokens - headerTokens - truncationMessageTokens;

        if (availableTokens <= 0)
        {
            return new TruncationResult($"{headerText}\n\n[Content truncated due to context window limit]", true, contentLines.Length);
        }

        var keptLines = new List<string>();
        var currentTokenCount = 0;
        foreach (var line in contentLines)
        {
            var lineTokens = EstimateTokens($"{line}\n");
            if (currentTokenCount + lineTokens > availableTokens)
            {
                break;
            }

            keptLines.Add(line);
            currentTokenCount += lineTokens;
        }

        var removedCount = contentLines.Length - keptLines.Count;
        var truncatedContent = string.Join('\n', headerLines.Concat(keptLines));
        return new TruncationResult($"{truncatedContent}\n\n[{removedCount} more lines truncated due to context window limit]", true, removedCount);
    }
}
