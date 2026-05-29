using System.Text.Json;

namespace Lfe.CommentChecker;

public static class Runner
{
    public static string? ResolveCommentCheckerBinary(string binaryName, string? cachedBinaryPath, Func<string, bool> existsSync)
    {
        if (cachedBinaryPath is not null && existsSync(cachedBinaryPath)) return cachedBinaryPath;
        return null;
    }

    public static async Task<CheckResult> RunCommentCheckerAsync(
        object hookInput, string? binaryPath, Func<string, bool> existsSync,
        Func<string[], (System.IO.Stream Stdin, string Stdout, string Stderr, Task<int> Exited, Action Kill)> spawn,
        string? customPrompt = null, int timeoutMs = 30_000)
    {
        if (binaryPath is null || !existsSync(binaryPath))
            return new CheckResult(false, "");

        var args = new List<string> { binaryPath, "check" };
        if (customPrompt is not null) { args.Add("--prompt"); args.Add(customPrompt); }

        var (stdin, stdout, stderr, exited, kill) = spawn(args.ToArray());
        var inputBytes = JsonSerializer.SerializeToUtf8Bytes(hookInput);
        await stdin.WriteAsync(inputBytes);
        await stdin.FlushAsync();
        stdin.Close();

        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var exitCode = await exited.WaitAsync(cts.Token);
            if (exitCode == 0) return new CheckResult(false, "");
            if (exitCode == 2) return new CheckResult(true, stderr);
            return new CheckResult(false, "");
        }
        catch (OperationCanceledException)
        {
            kill();
            return new CheckResult(false, "");
        }
    }
}
