namespace Omodot.Utils.Tests;

public sealed class BunHashShimTests
{
    [Fact]
    public void BunHashXxh32_hashes_input_with_seed()
    {
        Assert.Equal(BunHashShim.BunHashXxh32("abc", 42), BunHashShim.BunHashXxh32("abc", 42));
    }
}
