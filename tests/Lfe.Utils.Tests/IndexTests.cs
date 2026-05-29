namespace Lfe.Utils.Tests;

public sealed class IndexTests
{
    [Fact]
    public void LibrarySurface_is_usable()
    {
        Assert.Equal("1s", FormatDuration.Human(1000));
    }
}
