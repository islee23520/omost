namespace Lfe.Utils.Tests;

public sealed class ToolNameTests
{
    [Fact]
    public void Transform_maps_special_tools_and_pascalizes()
    {
        Assert.Equal("WebFetch", ToolName.Transform(" webfetch "));
        Assert.Equal("CallOmoAgent", ToolName.Transform("call-omo-agent"));
    }
}
