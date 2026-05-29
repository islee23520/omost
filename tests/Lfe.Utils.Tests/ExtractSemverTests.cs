namespace Lfe.Utils.Tests;

public sealed class ExtractSemverTests
{
    [Fact]
    public void FromOutput_extracts_semver()
    {
        Assert.Equal("1.2.3", ExtractSemver.FromOutput("tool v1.2.3"));
    }
}
