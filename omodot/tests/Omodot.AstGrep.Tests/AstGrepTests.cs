using Omodot.AstGrep;

namespace Omodot.AstGrep.Tests;

public sealed class AstGrepTests
{
    [Fact]
    public void ExposesLanguageAndDefaultConstants()
    {
        Assert.Contains("typescript", CliLanguages.All);
        Assert.Equal(300_000, CliLanguages.DefaultTimeoutMs);
        Assert.Equal(1 * 1024 * 1024, CliLanguages.DefaultMaxOutputBytes);
        Assert.Equal(500, CliLanguages.DefaultMaxMatches);
        Assert.Equal("typescript", CliLanguage.Typescript.ToCliName());
    }

    [Fact]
    public void BuildSgArgs_matches_typescript_behavior()
    {
        var args = SgRunner.BuildSgArgs(
            new SgRunArgs
            {
                Pattern = "$A",
                Language = CliLanguage.Typescript,
                Paths = ["src/a.ts", "src/b.ts"],
                Globs = ["**/*.ts", "**/*.tsx"],
                Rewrite = "foo",
                Context = 2,
            },
            new SgBuildFlags { IncludeJson = true, IncludeUpdateAll = true });

        Assert.Equal(
        [
            "run",
            "-p",
            "$A",
            "--lang",
            "typescript",
            "--json=compact",
            "-r",
            "foo",
            "--update-all",
            "-C",
            "2",
            "--globs",
            "**/*.ts",
            "--globs",
            "**/*.tsx",
            "--",
            "src/a.ts",
            "src/b.ts",
        ], args);

        Assert.Equal(
            ["run", "-p", "foo", "--lang", "python", "--", "."],
            SgRunner.BuildSgArgs(
                new SgRunArgs { Pattern = "foo", Language = CliLanguage.Python },
                new SgBuildFlags { IncludeJson = false, IncludeUpdateAll = false }));
    }

    [Fact]
    public void PatternHints_match_typescript_behavior()
    {
        Assert.Contains("regex escapes", PatternHints.DetectRegexMisuse("\\w+")!);
        Assert.Contains("character classes", PatternHints.DetectRegexMisuse("[a-z]")!);
        Assert.Contains("regex wildcards", PatternHints.DetectRegexMisuse("foo.*")!);
        Assert.Contains("alternation", PatternHints.DetectRegexMisuse("foo|bar")!);
        Assert.Null(PatternHints.DetectRegexMisuse("foo"));

        Assert.Equal(
            "Hint: Remove trailing colon. Try: \"class Foo\"",
            PatternHints.DetectLanguageSpecificMistake("class Foo:", CliLanguage.Python));
        Assert.Equal(
            "Hint: Remove trailing colon. Try: \"async def foo()\"",
            PatternHints.DetectLanguageSpecificMistake("async def foo():", CliLanguage.Python));
        Assert.Equal(
            "Hint: Function patterns need params and body. Try \"function $NAME($$$) { $$$ }\"",
            PatternHints.DetectLanguageSpecificMistake("function $NAME", CliLanguage.Javascript));
        Assert.Equal(
            "Hint: Go function patterns need params and body. Try \"func $NAME($$$) { $$$ }\"",
            PatternHints.DetectLanguageSpecificMistake("func $NAME", CliLanguage.Go));
        Assert.Equal(
            "Hint: Rust fn patterns need params and body. Try \"fn $NAME($$$) { $$$ }\"",
            PatternHints.DetectLanguageSpecificMistake("fn $NAME", CliLanguage.Rust));
        Assert.Null(PatternHints.DetectLanguageSpecificMistake("something else", CliLanguage.Typescript));
        Assert.Contains("alternation", PatternHints.GetPatternHint("foo|bar", CliLanguage.Go)!);
    }

    [Fact]
    public void Formatters_match_typescript_behavior()
    {
        Assert.Equal("No matches found", ResultFormatter.FormatSearchResult(new SgResult()));
        Assert.Equal("Error: boom", ResultFormatter.FormatSearchResult(new SgResult { Error = "boom" }));
        Assert.Contains(
            "src/file.ts:1:1",
            ResultFormatter.FormatSearchResult(CreateResult()));
        Assert.Contains(
            "output exceeded 1MB limit",
            ResultFormatter.FormatReplaceResult(CreateResult(truncated: true, truncatedReason: SgTruncatedReason.MaxOutputBytes), true));
        Assert.Contains(
            "[DRY RUN] 1 replacement(s):",
            ResultFormatter.FormatReplaceResult(CreateResult(), true));
    }

    [Fact]
    public void CreateSgResultFromStdout_handles_empty_truncation_and_salvage()
    {
        var empty = SgCompactJsonOutput.CreateSgResultFromStdout("   ");
        Assert.Empty(empty.Matches);
        Assert.False(empty.Truncated);

        var manyMatches = Enumerable.Range(0, CliLanguages.DefaultMaxMatches + 1)
            .Select(index => CreateMatch(file: $"src/{index}.ts", text: $"match-{index}", lines: $"match-{index}"))
            .ToArray();
        var maxMatchesResult = SgCompactJsonOutput.CreateSgResultFromStdout(System.Text.Json.JsonSerializer.Serialize(manyMatches));
        Assert.Equal(CliLanguages.DefaultMaxMatches, maxMatchesResult.Matches.Count);
        Assert.Equal(CliLanguages.DefaultMaxMatches + 1, maxMatchesResult.TotalMatches);
        Assert.True(maxMatchesResult.Truncated);
        Assert.Equal(SgTruncatedReason.MaxMatches, maxMatchesResult.TruncatedReason);

        var validFirst = CreateMatch(file: "src/one.ts", text: "one", lines: "one");
        var hugeSecond = CreateMatch(
            file: "src/two.ts",
            text: $"two{new string('x', CliLanguages.DefaultMaxOutputBytes)}",
            lines: $"two{new string('x', CliLanguages.DefaultMaxOutputBytes)}");
        var salvaged = SgCompactJsonOutput.CreateSgResultFromStdout(System.Text.Json.JsonSerializer.Serialize(new[] { validFirst, hugeSecond }));
        Assert.Single(salvaged.Matches);
        Assert.Equal("src/one.ts", salvaged.Matches[0].File);
        Assert.True(salvaged.Truncated);
        Assert.Equal(SgTruncatedReason.MaxOutputBytes, salvaged.TruncatedReason);

        var malformedFirst = "{\"text\":\"bad\\q\",\"range\":{\"byteOffset\":{\"start\":0,\"end\":3},\"start\":{\"line\":0,\"column\":0},\"end\":{\"line\":0,\"column\":3}},\"file\":\"src/bad.ts\",\"lines\":\"bad\",\"charCount\":{\"leading\":0,\"trailing\":0},\"language\":\"typescript\"}";
        var malformedHuge = $"{{\"text\":\"{new string('y', CliLanguages.DefaultMaxOutputBytes)}\",\"range\":{{\"byteOffset\":{{\"start\":0,\"end\":3}},\"start\":{{\"line\":0,\"column\":0}},\"end\":{{\"line\":0,\"column\":3}}}},\"file\":\"src/bad-2.ts\",\"lines\":\"bad\",\"charCount\":{{\"leading\":0,\"trailing\":0}},\"language\":\"typescript\"}}";
        var malformed = SgCompactJsonOutput.CreateSgResultFromStdout($"[{malformedFirst},{malformedHuge}]");
        Assert.Empty(malformed.Matches);
        Assert.True(malformed.Truncated);
        Assert.Equal(SgTruncatedReason.MaxOutputBytes, malformed.TruncatedReason);
        Assert.Equal("Output too large and could not be parsed", malformed.Error);

        var invalidJson = SgCompactJsonOutput.CreateSgResultFromStdout("not-json");
        Assert.Empty(invalidJson.Matches);
        Assert.False(invalidJson.Truncated);
    }

    [Fact]
    public async Task RunSgAsync_handles_lookup_spawn_and_writepass_paths()
    {
        var noBinary = await SgRunner.RunSgAsync(
            new SgRunArgs { Pattern = "foo", Language = CliLanguage.Typescript },
            new SgRunnerDeps
            {
                ResolveBinary = () => throw new TestCodeException("missing", "ENOENT"),
                SpawnProcess = (_, _, _) => Task.FromResult(new SpawnResult()),
            });
        Assert.Contains("ast-grep (sg) binary not found", noBinary.Error);

        var timeout = await SgRunner.RunSgAsync(
            new SgRunArgs { Pattern = "foo", Language = CliLanguage.Typescript },
            new SgRunnerDeps
            {
                ResolveBinary = () => Task.FromResult("sg"),
                SpawnProcess = (_, _, _) => throw new Exception("spawn timeout"),
            });
        Assert.True(timeout.Truncated);
        Assert.Equal(SgTruncatedReason.Timeout, timeout.TruncatedReason);

        var noFiles = await SgRunner.RunSgAsync(
            new SgRunArgs { Pattern = "foo", Language = CliLanguage.Typescript },
            new SgRunnerDeps
            {
                ResolveBinary = () => Task.FromResult("sg"),
                SpawnProcess = (_, _, _) => Task.FromResult(new SpawnResult { ExitCode = 1, Stderr = "No files found in project" }),
            });
        Assert.Empty(noFiles.Matches);
        Assert.Null(noFiles.Error);

        var calls = new List<IReadOnlyList<string>>();
        var output = System.Text.Json.JsonSerializer.Serialize(new[] { CreateMatch(text: "replaced text", lines: "replaced text") });
        var replaceResult = await SgRunner.RunSgAsync(
            new SgRunArgs
            {
                Pattern = "foo",
                Language = CliLanguage.Typescript,
                Rewrite = "bar",
                UpdateAll = true,
                Paths = ["src"],
            },
            new SgRunnerDeps
            {
                ResolveBinary = () => Task.FromResult("sg"),
                SpawnProcess = (_, args, _) =>
                {
                    calls.Add(args);
                    return Task.FromResult(calls.Count == 1
                        ? new SpawnResult { Stdout = output, ExitCode = 0 }
                        : new SpawnResult { ExitCode = 0 });
                },
            });

        Assert.Single(replaceResult.Matches);
        Assert.Equal(2, calls.Count);
        Assert.Contains("--json=compact", calls[0]);
        Assert.DoesNotContain("--update-all", calls[0]);
        Assert.Contains("--update-all", calls[1]);
        Assert.DoesNotContain("--json=compact", calls[1]);

        var writeFailure = await SgRunner.RunSgAsync(
            new SgRunArgs { Pattern = "foo", Language = CliLanguage.Typescript, Rewrite = "bar", UpdateAll = true },
            new SgRunnerDeps
            {
                ResolveBinary = () => Task.FromResult("sg"),
                SpawnProcess = (_, args, _) => Task.FromResult(args.Contains("--json=compact")
                    ? new SpawnResult { Stdout = System.Text.Json.JsonSerializer.Serialize(new[] { CreateMatch() }), ExitCode = 0 }
                    : new SpawnResult { ExitCode = 1, Stderr = "replace failed" }),
            });
        Assert.Contains("Replace failed: replace failed", writeFailure.Error);
    }

    private static SgResult CreateResult(bool truncated = false, SgTruncatedReason? truncatedReason = null)
    {
        return new SgResult
        {
            Matches = [CreateMatch()],
            TotalMatches = 1,
            Truncated = truncated,
            TruncatedReason = truncatedReason,
        };
    }

    private static CliMatch CreateMatch(string file = "src/file.ts", string text = "const foo = 1", string lines = "const foo = 1")
    {
        return new CliMatch
        {
            Text = text,
            File = file,
            Lines = lines,
            Language = "typescript",
            Range = new CliRange
            {
                ByteOffset = new ByteOffsetRange { Start = 0, End = 13 },
                Start = new Position { Line = 0, Column = 0 },
                End = new Position { Line = 0, Column = 13 },
            },
            CharCount = new CharCount { Leading = 0, Trailing = 0 },
        };
    }

    private sealed class TestCodeException(string message, string code) : Exception(message)
    {
        public string Code { get; } = code;
    }
}
