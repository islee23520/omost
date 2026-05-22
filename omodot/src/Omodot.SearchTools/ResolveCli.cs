using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Omodot.SearchTools;

public sealed record ResolveCliOptions
{
    public Func<string, string?>? Which { get; init; }

    public string? Platform { get; init; }
}

public static class ResolveCli
{
    public const int DEFAULT_RG_THREADS = 4;

    public static ResolvedSearchCli ResolveGlobCli(ResolveCliOptions? options = null)
    {
        var which = options?.Which ?? DefaultWhich;
        var platform = options?.Platform ?? GetPlatform();

        var rg = which("rg");
        if (!string.IsNullOrWhiteSpace(rg))
        {
            return new ResolvedSearchCli(rg, SearchBackend.Rg);
        }

        if (string.Equals(platform, "win32", StringComparison.Ordinal))
        {
            return new ResolvedSearchCli("powershell.exe", SearchBackend.PowerShell);
        }

        var find = which("find");
        if (!string.IsNullOrWhiteSpace(find))
        {
            return new ResolvedSearchCli(find, SearchBackend.Find);
        }

        throw new InvalidOperationException("ripgrep (rg) or find binary not found in PATH");
    }

    public static ResolvedSearchCli ResolveGrepCli(ResolveCliOptions? options = null)
    {
        var which = options?.Which ?? DefaultWhich;

        var rg = which("rg");
        if (!string.IsNullOrWhiteSpace(rg))
        {
            return new ResolvedSearchCli(rg, SearchBackend.Rg);
        }

        var grep = which("grep");
        if (!string.IsNullOrWhiteSpace(grep))
        {
            return new ResolvedSearchCli(grep, SearchBackend.Grep);
        }

        throw new InvalidOperationException("ripgrep (rg) or grep binary not found in PATH");
    }

    internal static string? DefaultWhich(string commandName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        var pathHit = FindOnPath(commandName);
        if (!string.IsNullOrWhiteSpace(pathHit))
        {
            return pathHit;
        }

        if (!OperatingSystem.IsWindows())
        {
            return WhichOnUnix(commandName);
        }

        return null;
    }

    private static string GetPlatform()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win32" : "unix";
    }

    private static string? FindOnPath(string commandName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var candidate in GetCandidates(directory, commandName))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidates(string directory, string commandName)
    {
        yield return Path.Combine(directory, commandName);

        if (OperatingSystem.IsWindows())
        {
            if (!commandName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.Combine(directory, commandName + ".exe");
            }

            if (!commandName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.Combine(directory, commandName + ".cmd");
            }

            if (!commandName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.Combine(directory, commandName + ".bat");
            }
        }
    }

    private static string? WhichOnUnix(string commandName)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            process.StartInfo.ArgumentList.Add(commandName);
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
