namespace Lfe.Utils.Tests;

public sealed class FrontmatterTests
{
    [Fact]
    public void Parse_reads_yaml_frontmatter()
    {
        var result = Frontmatter.Parse("---\ndescription: Test command\n---\nBody content");

        Assert.True(result.HadFrontmatter);
        Assert.False(result.ParseError);
        Assert.NotNull(result.Data);
        Assert.Equal("Test command", result.Data["description"]!.GetValue<string>());
        Assert.Equal("Body content", result.Body);
    }

    [Fact]
    public void Parse_generic_materializes_type()
    {
        var result = Frontmatter.Parse<TestFrontmatter>("---\ndescription: Test command\n---\nBody");
        Assert.Equal("Test command", result.Data!.Description);
    }

    private sealed record TestFrontmatter(string Description);
}
