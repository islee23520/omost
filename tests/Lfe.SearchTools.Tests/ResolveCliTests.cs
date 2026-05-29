using Lfe.SearchTools;

namespace Lfe.SearchTools.Tests;

public sealed class ResolveCliTests
{
    [Fact]
    public void ResolveGlobCliPrefersRipgrep()
    {
        var cli = ResolveCli.ResolveGlobCli(new ResolveCliOptions
        {
            Which = name => name == "rg" ? "/usr/bin/rg" : null,
            Platform = "unix",
        });

        Assert.Equal(SearchBackend.Rg, cli.Backend);
        Assert.Equal("/usr/bin/rg", cli.Path);
    }

    [Fact]
    public void ResolveGlobCliFallsBackToPowerShellOnWindows()
    {
        var cli = ResolveCli.ResolveGlobCli(new ResolveCliOptions
        {
            Which = _ => null,
            Platform = "win32",
        });

        Assert.Equal(SearchBackend.PowerShell, cli.Backend);
        Assert.Equal("powershell.exe", cli.Path);
    }

    [Fact]
    public void ResolveGrepCliFallsBackToClassicGrep()
    {
        var cli = ResolveCli.ResolveGrepCli(new ResolveCliOptions
        {
            Which = name => name == "grep" ? "/usr/bin/grep" : null,
        });

        Assert.Equal(SearchBackend.Grep, cli.Backend);
        Assert.Equal("/usr/bin/grep", cli.Path);
    }
}
