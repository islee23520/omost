using System.Reflection;

namespace Omodot.AstGrep;

public sealed record SgRunArgs
{
    public string Pattern { get; init; } = string.Empty;

    public CliLanguage Language { get; init; }

    public string? Cwd { get; init; }

    public IReadOnlyList<string>? Paths { get; init; }

    public IReadOnlyList<string>? Globs { get; init; }

    public string? Rewrite { get; init; }

    public int? Context { get; init; }

    public bool UpdateAll { get; init; }
}

public sealed record SgBuildFlags
{
    public bool IncludeJson { get; init; }

    public bool IncludeUpdateAll { get; init; }
}

public sealed record SpawnOptions
{
    public string? Cwd { get; init; }

    public string? Stdout { get; init; }

    public string? Stderr { get; init; }
}

public sealed record SpawnResult
{
    public string Stdout { get; init; } = string.Empty;

    public string Stderr { get; init; } = string.Empty;

    public int ExitCode { get; init; }
}

public delegate Task<SpawnResult> SpawnProcess(string binary, IReadOnlyList<string> args, SpawnOptions? options);

public sealed record SgRunnerDeps
{
    public Func<Task<string>> ResolveBinary { get; init; } = null!;

    public SpawnProcess SpawnProcess { get; init; } = null!;
}

public static class SgRunner
{
    private const string SgBinaryNotFoundMessage = "ast-grep (sg) binary not found.\n\nInstall options:\n  bun add -D @ast-grep/cli\n  cargo install ast-grep --locked\n  brew install ast-grep";

    public static string[] BuildSgArgs(SgRunArgs options, SgBuildFlags flags)
    {
        var args = new List<string>
        {
            "run",
            "-p",
            options.Pattern,
            "--lang",
            options.Language.ToCliName(),
        };

        if (flags.IncludeJson)
        {
            args.Add("--json=compact");
        }

        if (!string.IsNullOrEmpty(options.Rewrite))
        {
            args.Add("-r");
            args.Add(options.Rewrite);

            if (flags.IncludeUpdateAll)
            {
                args.Add("--update-all");
            }
        }

        if (options.Context is > 0)
        {
            args.Add("-C");
            args.Add(options.Context.Value.ToString());
        }

        if (options.Globs is not null)
        {
            foreach (var glob in options.Globs)
            {
                args.Add("--globs");
                args.Add(glob);
            }
        }

        var paths = options.Paths is { Count: > 0 } ? options.Paths : ["."];
        args.Add("--");
        args.AddRange(paths);
        return [.. args];
    }

    public static async Task<SgResult> RunSgAsync(SgRunArgs options, SgRunnerDeps deps)
    {
        var shouldSeparateWritePass = !string.IsNullOrEmpty(options.Rewrite) && options.UpdateAll;
        var args = BuildSgArgs(options, new SgBuildFlags { IncludeJson = true, IncludeUpdateAll = false });

        string binary;
        try
        {
            binary = await deps.ResolveBinary().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            return new SgResult
            {
                Matches = Array.Empty<CliMatch>(),
                TotalMatches = 0,
                Truncated = false,
                Error = IsNoEntryError(error) ? SgBinaryNotFoundMessage : $"Failed to resolve ast-grep binary: {ErrorMessage(error)}",
            };
        }

        var searchResult = await TrySpawnAsync(binary, args, options.Cwd, deps).ConfigureAwait(false);
        if (searchResult.Error is not null)
        {
            return searchResult.Error;
        }

        var output = searchResult.Value!;
        if (output.ExitCode != 0 && string.IsNullOrWhiteSpace(output.Stdout))
        {
            if (output.Stderr.Contains("No files found", StringComparison.Ordinal))
            {
                return new SgResult { Matches = Array.Empty<CliMatch>(), TotalMatches = 0, Truncated = false };
            }

            if (!string.IsNullOrWhiteSpace(output.Stderr))
            {
                return new SgResult { Matches = Array.Empty<CliMatch>(), TotalMatches = 0, Truncated = false, Error = output.Stderr.Trim() };
            }

            return new SgResult { Matches = Array.Empty<CliMatch>(), TotalMatches = 0, Truncated = false };
        }

        var jsonResult = SgCompactJsonOutput.CreateSgResultFromStdout(output.Stdout);
        if (!(shouldSeparateWritePass && jsonResult.Matches.Count > 0))
        {
            return jsonResult;
        }

        var writeArgs = BuildSgArgs(options, new SgBuildFlags { IncludeJson = false, IncludeUpdateAll = true });
        var writeResult = await TrySpawnAsync(binary, writeArgs, options.Cwd, deps).ConfigureAwait(false);
        if (writeResult.Error is not null)
        {
            return jsonResult with { Error = $"Replace failed: {writeResult.Error.Error ?? "unknown error"}" };
        }

        if (writeResult.Value!.ExitCode != 0)
        {
            var errorDetail = string.IsNullOrWhiteSpace(writeResult.Value.Stderr)
                ? $"ast-grep exited with code {writeResult.Value.ExitCode}"
                : writeResult.Value.Stderr.Trim();
            return jsonResult with { Error = $"Replace failed: {errorDetail}" };
        }

        return jsonResult;
    }

    private static async Task<SpawnOutcome> TrySpawnAsync(string binary, IReadOnlyList<string> args, string? cwd, SgRunnerDeps deps)
    {
        try
        {
            var value = await deps.SpawnProcess(binary, args, new SpawnOptions
            {
                Cwd = cwd,
                Stdout = "pipe",
                Stderr = "pipe",
            }).ConfigureAwait(false);

            return new SpawnOutcome { Value = value };
        }
        catch (Exception error)
        {
            if (error.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                return new SpawnOutcome
                {
                    Error = new SgResult
                    {
                        Matches = Array.Empty<CliMatch>(),
                        TotalMatches = 0,
                        Truncated = true,
                        TruncatedReason = SgTruncatedReason.Timeout,
                        Error = error.Message,
                    },
                };
            }

            if (IsNoEntryError(error))
            {
                return new SpawnOutcome
                {
                    Error = new SgResult
                    {
                        Matches = Array.Empty<CliMatch>(),
                        TotalMatches = 0,
                        Truncated = false,
                        Error = SgBinaryNotFoundMessage,
                    },
                };
            }

            return new SpawnOutcome
            {
                Error = new SgResult
                {
                    Matches = Array.Empty<CliMatch>(),
                    TotalMatches = 0,
                    Truncated = false,
                    Error = $"Failed to spawn ast-grep: {ErrorMessage(error)}",
                },
            };
        }
    }

    private static bool IsNoEntryError(Exception error)
    {
        var code = TryGetCode(error);
        var message = ErrorMessage(error);
        return string.Equals(code, "ENOENT", StringComparison.Ordinal) ||
               message.Contains("ENOENT", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetCode(Exception error)
    {
        var property = error.GetType().GetProperty("Code", BindingFlags.Public | BindingFlags.Instance);
        if (property?.GetValue(error) is not null)
        {
            return Convert.ToString(property.GetValue(error));
        }

        var field = error.GetType().GetField("Code", BindingFlags.Public | BindingFlags.Instance);
        if (field?.GetValue(error) is not null)
        {
            return Convert.ToString(field.GetValue(error));
        }

        if (error.Data.Contains("code"))
        {
            return Convert.ToString(error.Data["code"]);
        }

        return null;
    }

    private static string ErrorMessage(Exception error)
    {
        return error.Message;
    }

    private sealed record SpawnOutcome
    {
        public SpawnResult? Value { get; init; }

        public SgResult? Error { get; init; }
    }
}
