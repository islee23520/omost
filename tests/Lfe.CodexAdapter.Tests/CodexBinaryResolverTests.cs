using Xunit;

namespace Lfe.CodexAdapter.Tests;

public sealed class CodexBinaryResolverTests
{
    private readonly CodexBinaryResolver _resolver = new();

    [Fact]
    public void Resolve_ExplicitPath_ReturnsPath()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var options = new CodexAdapterOptions { CodexBinaryPath = tempFile };
            var result = _resolver.Resolve(options);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Resolve_ExplicitPathNotFound_ThrowsInvalidOperation()
    {
        var options = new CodexAdapterOptions { CodexBinaryPath = "/nonexistent/codex-binary" };
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.Resolve(options));
        Assert.Contains("explicit path", ex.Message);
        Assert.Contains("/nonexistent/codex-binary", ex.Message);
    }

    [Fact]
    public void Resolve_NoExplicitNoEnv_ResolvesFromPathOrThrows()
    {
        var options = new CodexAdapterOptions();
        var original = Environment.GetEnvironmentVariable(CodexTransportConstants.CodexBinaryEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(CodexTransportConstants.CodexBinaryEnvVar, null);
            var result = _resolver.Resolve(options);
            Assert.NotNull(result);
            Assert.True(File.Exists(result));
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            if (original is not null)
                Environment.SetEnvironmentVariable(CodexTransportConstants.CodexBinaryEnvVar, original);
        }
    }

    [Fact]
    public void Resolve_EnvVarSet_ReturnsEnvVarPath()
    {
        var tempFile = Path.GetTempFileName();
        var original = Environment.GetEnvironmentVariable(CodexTransportConstants.CodexBinaryEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(CodexTransportConstants.CodexBinaryEnvVar, tempFile);
            var options = new CodexAdapterOptions();
            var result = _resolver.Resolve(options);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CodexTransportConstants.CodexBinaryEnvVar, original);
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Resolve_ExplicitPathWinsOverEnvVar()
    {
        var explicitFile = Path.GetTempFileName();
        var envFile = Path.GetTempFileName();
        var original = Environment.GetEnvironmentVariable(CodexTransportConstants.CodexBinaryEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(CodexTransportConstants.CodexBinaryEnvVar, envFile);
            var options = new CodexAdapterOptions { CodexBinaryPath = explicitFile };
            var result = _resolver.Resolve(options);
            Assert.Equal(explicitFile, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CodexTransportConstants.CodexBinaryEnvVar, original);
            File.Delete(explicitFile);
            File.Delete(envFile);
        }
    }

    [Fact]
    public void ResolveConfig_SetsDefaults()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var options = new CodexAdapterOptions { CodexBinaryPath = tempFile };
            var config = _resolver.ResolveConfig(options);
            Assert.Equal(tempFile, config.BinaryPath);
            Assert.Equal(Directory.GetCurrentDirectory(), config.WorkingDirectory);
            Assert.Equal(CodexTransportConstants.DefaultTimeoutMs, config.TimeoutMs);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ResolveConfig_CustomOptionsApplied()
    {
        var tempFile = Path.GetTempFileName();
        var tempDir = Path.GetTempPath();
        try
        {
            var options = new CodexAdapterOptions
            {
                CodexBinaryPath = tempFile,
                WorkingDirectory = tempDir,
                TimeoutMs = 5000,
                EnvironmentOverrides = new Dictionary<string, string> { ["KEY"] = "value" },
            };
            var config = _resolver.ResolveConfig(options);
            Assert.Equal(tempFile, config.BinaryPath);
            Assert.Equal(tempDir, config.WorkingDirectory);
            Assert.Equal(5000, config.TimeoutMs);
            Assert.Single(config.EnvironmentOverrides);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ResolveConfig_InvalidWorkingDir_Throws()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var options = new CodexAdapterOptions
            {
                CodexBinaryPath = tempFile,
                WorkingDirectory = "/nonexistent/directory/xyz",
            };
            var ex = Assert.Throws<InvalidOperationException>(() => _resolver.ResolveConfig(options));
            Assert.Contains("Working directory", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
