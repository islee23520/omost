namespace Omodot.CommandExecutor.Tests;

public sealed class HomeDirectoryTests
{
    [Fact]
    public async Task GetHomeDirectory_prefers_home_over_userprofile()
    {
        var result = await EnvironmentScope.RunAsync(() =>
        {
            Environment.SetEnvironmentVariable("HOME", "/tmp/home-value");
            Environment.SetEnvironmentVariable("USERPROFILE", "/tmp/userprofile-value");
            return Task.FromResult(HomeDirectory.GetHomeDirectory());
        });

        Assert.Equal("/tmp/home-value", result);
    }

    [Fact]
    public async Task GetHomeDirectory_uses_userprofile_when_home_is_not_set()
    {
        var result = await EnvironmentScope.RunAsync(() =>
        {
            Environment.SetEnvironmentVariable("HOME", null);
            Environment.SetEnvironmentVariable("USERPROFILE", "/tmp/userprofile-value");
            return Task.FromResult(HomeDirectory.GetHomeDirectory());
        });

        Assert.Equal("/tmp/userprofile-value", result);
    }
}
