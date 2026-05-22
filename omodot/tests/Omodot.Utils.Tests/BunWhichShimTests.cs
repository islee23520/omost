namespace Omodot.Utils.Tests;

public sealed class BunWhichShimTests
{
    [Fact]
    public void BunWhich_resolves_existing_command_and_rejects_unsafe_name()
    {
        Assert.NotNull(BunWhichShim.BunWhich("dotnet"));
        Assert.Null(BunWhichShim.Which("../evil"));
    }
}
