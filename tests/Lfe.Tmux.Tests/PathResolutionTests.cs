using System.Runtime.InteropServices;

namespace Lfe.Tmux.Tests;

public sealed class PathResolutionTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"lfe-tmux-{Guid.NewGuid():N}");

    public PathResolutionTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task UsesTmuxBinOverrideWhenProvided()
    {
        var result = await TmuxPathResolver.GetTmuxPathAsync(new Dictionary<string, string?>
        {
            ["TMUX_BIN"] = "/custom/tmux",
            ["PATH"] = "/ignored",
        });

        Assert.Equal("/custom/tmux", result);
    }

    [Fact]
    public async Task FindsTmuxOnPath()
    {
        var executablePath = Path.Combine(_tempDirectory, "tmux");
        await File.WriteAllTextAsync(executablePath, "#!/bin/sh\nexit 0\n");
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(executablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var result = await TmuxPathResolver.GetTmuxPathAsync(new Dictionary<string, string?>
        {
            ["PATH"] = _tempDirectory,
        });

        Assert.Equal(executablePath, result);
    }

    [Fact]
    public async Task ReturnsNullWhenTmuxIsMissing()
    {
        var result = await TmuxPathResolver.GetTmuxPathAsync(new Dictionary<string, string?>
        {
            ["PATH"] = "/definitely/missing",
        });

        Assert.Null(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
