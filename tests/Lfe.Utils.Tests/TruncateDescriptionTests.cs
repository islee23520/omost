namespace Lfe.Utils.Tests;

public sealed class TruncateDescriptionTests
{
    [Fact]
    public void Apply_truncates_long_text()
    {
        var input = new string('a', 130);
        var result = TruncateDescription.Apply(input);
        Assert.Equal(120, result.Length);
        Assert.EndsWith("...", result, StringComparison.Ordinal);
    }
}
