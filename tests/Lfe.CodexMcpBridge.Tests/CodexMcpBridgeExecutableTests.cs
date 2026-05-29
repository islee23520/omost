using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Lfe.CodexMcpBridge.Tests;

public sealed class CodexMcpBridgeExecutableTests
{
    [Fact]
    public async Task BuiltBridgeDll_RunsMcpInitializeAndToolsListOverStdio()
    {
        var bridgeDll = Path.Combine(AppContext.BaseDirectory, "Lfe.CodexMcpBridge.dll");
        var bridgeRuntimeConfig = Path.Combine(AppContext.BaseDirectory, "Lfe.CodexMcpBridge.runtimeconfig.json");
        Assert.True(File.Exists(bridgeDll), bridgeDll);
        Assert.True(File.Exists(bridgeRuntimeConfig), bridgeRuntimeConfig);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add(bridgeDll);
        process.StartInfo.ArgumentList.Add("mcp");
        process.StartInfo.Environment["CODEX_BINARY_PATH"] = "/usr/bin/echo";
        process.StartInfo.Environment["CODEX_WORKING_DIRECTORY"] = Path.GetTempPath();

        Assert.True(process.Start());
        await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}");
        await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}");
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(10_000), stderr);
        Assert.Equal(0, process.ExitCode);

        var lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);

        using var initialize = JsonDocument.Parse(lines[0]);
        Assert.Equal(1, initialize.RootElement.GetProperty("id").GetInt64());
        Assert.Equal("lfe_codex", initialize.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());

        using var toolsList = JsonDocument.Parse(lines[1]);
        Assert.Equal(2, toolsList.RootElement.GetProperty("id").GetInt64());
        Assert.Equal(4, toolsList.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength());
    }
}
