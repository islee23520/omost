namespace Omodot.CommandExecutor.Tests;

public sealed class ShellPathFinderTests
{
    [Fact]
    public void FindZshPath_returns_existing_custom_path_before_defaults()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("omodot-shell-path-");

        try
        {
            var zshPath = Path.Combine(tempDirectory.FullName, "zsh");
            File.WriteAllText(zshPath, string.Empty);

            Assert.Equal(zshPath, ShellPathFinder.FindZshPath(zshPath));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void FindZshPath_ignores_missing_custom_path_and_returns_default_when_available()
    {
        var expected = new[] { "/bin/zsh", "/usr/bin/zsh", "/usr/local/bin/zsh" }.FirstOrDefault(File.Exists);

        Assert.Equal(expected, ShellPathFinder.FindZshPath("/missing/zsh"));
    }

    [Fact]
    public void FindBashPath_returns_first_available_default_bash_path()
    {
        var expected = new[] { "/bin/bash", "/usr/bin/bash", "/usr/local/bin/bash" }.FirstOrDefault(File.Exists);

        Assert.Equal(expected, ShellPathFinder.FindBashPath());
    }
}
