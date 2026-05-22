namespace Omodot.Utils.Tests;

public sealed class DynamicTruncatorTests
{
    [Fact]
    public void EstimateTokens_uses_four_chars_per_token()
    {
        Assert.Equal(2, DynamicTruncator.EstimateTokens("abcde"));
    }

    [Fact]
    public void TruncateToTokenLimit_preserves_headers()
    {
        var input = string.Join('\n', new[] { "header", "content line 1", "content line 2", "content line 3", "content line 4", "content line 5" });
        var result = DynamicTruncator.TruncateToTokenLimit(input, 10, 1);
        Assert.True(result.Truncated);
        Assert.Contains("header", result.Result, StringComparison.Ordinal);
    }
}
