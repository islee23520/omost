using Lfe.HashLine;

namespace Lfe.HashLine.Tests;

public sealed class ValidationTests
{
    [Fact]
    public void ParseLineRefUnderstandsCopiedMarkers()
    {
        var result = Validation.ParseLineRef(">>> 42#VK|const value = 1");
        Assert.Equal(new LineRef(42, "VK"), result);
    }

    [Fact]
    public void ParseLineRefRejectsLiteralPrefixes()
    {
        var error = Assert.Throws<ArgumentException>(() => Validation.ParseLineRef("LINE#HK"));
        Assert.Contains("not a line number", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateLineRefAcceptsLegacyHashes()
    {
        var lines = new[] { "  function hello() {", "    return 42", "  }" };
        var legacyHash = HashComputation.ComputeLegacyLineHash(1, lines[0]);
        Validation.ValidateLineRef(lines, $"1#{legacyHash}");
    }

    [Fact]
    public void ValidateLineRefsShowsMismatchContext()
    {
        var lines = new[] { "one", "two", "three", "four" };
        var error = Assert.Throws<Validation.HashlineMismatchError>(() => Validation.ValidateLineRefs(lines, ["2#ZZ"]));
        Assert.Contains(">>> 2#", error.Message, StringComparison.Ordinal);
        Assert.Contains("|two", error.Message, StringComparison.Ordinal);
    }
}
