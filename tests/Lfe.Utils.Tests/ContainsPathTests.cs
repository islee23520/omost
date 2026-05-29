namespace Lfe.Utils.Tests;

public sealed class ContainsPathTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"lfe-utils-path-{Guid.NewGuid():N}");

    public ContainsPathTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Check_returns_true_for_nested_candidate()
    {
        var root = Path.Combine(_tempDirectory, "project");
        var nested = Path.Combine(root, "nested", "file.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.WriteAllText(nested, "x");

        Assert.True(ContainsPath.Check(root, nested));
    }

    [Fact]
    public void IsWithinProject_returns_false_for_sibling()
    {
        var root = Path.Combine(_tempDirectory, "project");
        var sibling = Path.Combine(_tempDirectory, "sibling.txt");
        Directory.CreateDirectory(root);
        File.WriteAllText(sibling, "x");

        Assert.False(ContainsPath.IsWithinProject(sibling, root));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }
}
