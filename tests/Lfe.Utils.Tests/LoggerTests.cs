namespace Lfe.Utils.Tests;

public sealed class LoggerTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"lfe-utils-logger-{Guid.NewGuid():N}");

    public LoggerTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        Logger.ResetLoggerForTesting();
    }

    [Fact]
    public void Log_writes_when_flushed()
    {
        var filePath = Path.Combine(_tempDirectory, "log.txt");
        Logger.SetLoggerForTesting(new LoggerTestOverrides(filePath, 1024, 2));
        Logger.Log("hello", new { ok = true });
        Logger.FlushForTesting();
        var contents = File.ReadAllText(filePath);
        Assert.Contains("hello", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void GetLogFilePath_returns_current_path()
    {
        var filePath = Path.Combine(_tempDirectory, "log.txt");
        Logger.SetLoggerForTesting(new LoggerTestOverrides(filePath, 1024, 2));
        Assert.Equal(filePath, Logger.GetLogFilePath());
    }

    [Fact]
    public void ResetLoggerForTesting_restores_default_path()
    {
        Logger.SetLoggerForTesting(new LoggerTestOverrides(Path.Combine(_tempDirectory, "custom.txt"), 10, 1));
        Logger.ResetLoggerForTesting();
        Assert.Equal(Path.Combine(Path.GetTempPath(), "omo.log"), Logger.GetLogFilePath());
    }

    public void Dispose()
    {
        Logger.ResetLoggerForTesting();
        Directory.Delete(_tempDirectory, true);
    }
}
