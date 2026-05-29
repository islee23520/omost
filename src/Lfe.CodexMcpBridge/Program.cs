using Lfe.CodexAdapter;

namespace Lfe.CodexMcpBridge;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 2;
        }

        var command = args[0].ToLowerInvariant();
        return command switch
        {
            "mcp" => await RunMcpServerAsync(),
            "hook" => RunHook(args.Length > 1 ? args[1] : null),
            "init-plugin" => RunInitPlugin(args),
            "--help" or "-h" or "help" => PrintHelp(),
            "--version" or "-v" => PrintVersion(),
            _ => PrintUnknownCommand(command)
        };
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: lfe <command> [options]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Commands:");
        Console.Error.WriteLine("  mcp                       Start MCP stdio server");
        Console.Error.WriteLine("  hook <event>              Handle a Codex hook event");
        Console.Error.WriteLine("  init-plugin [--output DIR] Generate Codex plugin directory structure");
        Console.Error.WriteLine("  --help                    Show this help message");
        Console.Error.WriteLine("  --version                 Show version");
    }

    private static int PrintHelp()
    {
        PrintUsage();
        return 0;
    }

    private static int PrintVersion()
    {
        Console.WriteLine("lfe 0.1.0");
        return 0;
    }

    private static int PrintUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 2;
    }

    private static async Task<int> RunMcpServerAsync()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += HandleCancelKeyPress;

        try
        {
            using var server = new CodexMcpToolServer(CreateConfigFromEnvironment());
            await CodexMcpStdioServer.RunLineDelimitedJsonAsync(
                Console.In,
                Console.Out,
                server,
                cancellationTokenSource.Token).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"lfe codex mcp bridge terminated: {exception}").ConfigureAwait(false);
            await Console.Error.FlushAsync().ConfigureAwait(false);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= HandleCancelKeyPress;
        }

        void HandleCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        }
    }

    private static int RunHook(string? hookEvent)
    {
        if (hookEvent is null)
        {
            Console.Error.WriteLine("Usage: lfe hook <event>");
            Console.Error.WriteLine("Events: session-start, user-prompt-submit, post-tool-use, post-compact");
            return 2;
        }

        return CodexHookDispatcher.Dispatch(hookEvent, Console.In, Console.Out);
    }

    private static int RunInitPlugin(string[] args)
    {
        string? outputPath = null;
        for (var i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
            {
                outputPath = args[++i];
            }
        }

        outputPath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex", "local-marketplaces", "lfe-local");

        return CodexPluginPackager.Generate(outputPath, Console.Out);
    }

    private static CodexResolvedConfig CreateConfigFromEnvironment()
    {
        var binaryPath = Environment.GetEnvironmentVariable(CodexTransportConstants.CodexBinaryEnvVar);
        if (string.IsNullOrWhiteSpace(binaryPath)) binaryPath = CodexTransportConstants.CodexBinaryName;

        var workingDirectory = Environment.GetEnvironmentVariable("CODEX_WORKING_DIRECTORY");
        if (string.IsNullOrWhiteSpace(workingDirectory)) workingDirectory = Directory.GetCurrentDirectory();

        var timeoutMs = CodexTransportConstants.DefaultTimeoutMs;
        var timeoutOverride = Environment.GetEnvironmentVariable("CODEX_TIMEOUT_MS");
        if (!string.IsNullOrWhiteSpace(timeoutOverride) && int.TryParse(timeoutOverride, out var parsedTimeoutMs) && parsedTimeoutMs > 0)
        {
            timeoutMs = parsedTimeoutMs;
        }

        return new CodexResolvedConfig(
            binaryPath,
            workingDirectory,
            timeoutMs,
            new Dictionary<string, string>(),
            new CodexSessionOptions());
    }
}
