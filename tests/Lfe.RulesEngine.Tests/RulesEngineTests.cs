using Lfe.RulesEngine;
using Xunit;

namespace Lfe.RulesEngine.Tests;

public class RuleParserTests
{
    [Fact]
    public void ParseRuleFrontmatter_NoFrontmatter_ReturnsBodyAsIs()
    {
        var result = RuleParser.ParseRuleFrontmatter("Hello world");
        Assert.Equal("Hello world", result.Body);
        Assert.Null(result.Metadata.Description);
    }

    [Fact]
    public void ParseRuleFrontmatter_WithDescription_ExtractsMetadata()
    {
        var content = "---\ndescription: My rule\n---\nRule body here";
        var result = RuleParser.ParseRuleFrontmatter(content);
        Assert.Equal("My rule", result.Metadata.Description);
        Assert.Contains("Rule body here", result.Body);
    }

    [Fact]
    public void ParseRuleFrontmatter_AlwaysApply_Extracted()
    {
        var content = "---\nalwaysApply: true\n---\nBody";
        var result = RuleParser.ParseRuleFrontmatter(content);
        Assert.True(result.Metadata.AlwaysApply);
    }

    [Fact]
    public void ParseRuleFrontmatter_WithGlobs_Extracted()
    {
        var content = "---\nglobs: *.ts\n---\nBody";
        var result = RuleParser.ParseRuleFrontmatter(content);
        Assert.NotNull(result.Metadata.Globs);
        Assert.Equal("*.ts", result.Metadata.Globs[0]);
    }

    [Fact]
    public void ParseRuleFrontmatter_MultilineArray()
    {
        var content = "---\nglobs:\n  - *.ts\n  - *.tsx\n---\nBody";
        var result = RuleParser.ParseRuleFrontmatter(content);
        Assert.NotNull(result.Metadata.Globs);
        Assert.Equal(2, result.Metadata.Globs.Length);
        Assert.Equal("*.ts", result.Metadata.Globs[0]);
        Assert.Equal("*.tsx", result.Metadata.Globs[1]);
    }

    [Fact]
    public void ParseRuleFrontmatter_BOMStripped()
    {
        var content = "\uFEFF---\ndescription: test\n---\nBody";
        var result = RuleParser.ParseRuleFrontmatter(content);
        Assert.Equal("test", result.Metadata.Description);
    }

    [Fact]
    public void ParseRuleFrontmatter_UnclosedDelimiter_ReturnsBodyAsIs()
    {
        var content = "---\ndescription: test\nNo closing delimiter";
        var result = RuleParser.ParseRuleFrontmatter(content);
        Assert.Null(result.Metadata.Description);
    }
}

public class RuleMatcherTests
{
    [Fact]
    public void ShouldApplyRule_AlwaysApply_ReturnsTrue()
    {
        var meta = new RuleMetadata(AlwaysApply: true);
        var result = RuleMatcher.ShouldApplyRule(meta, "/foo/bar.ts", "/foo");
        Assert.True(result.Applies);
        Assert.Equal("alwaysApply", result.Reason);
    }

    [Fact]
    public void ShouldApplyRule_GlobMatch_ReturnsTrue()
    {
        var meta = new RuleMetadata(Globs: ["*.ts"]);
        var result = RuleMatcher.ShouldApplyRule(meta, "/project/src/file.ts", "/project");
        Assert.True(result.Applies);
    }

    [Fact]
    public void ShouldApplyRule_GlobNoMatch_ReturnsFalse()
    {
        var meta = new RuleMetadata(Globs: ["*.py"]);
        var result = RuleMatcher.ShouldApplyRule(meta, "/project/src/file.ts", "/project");
        Assert.False(result.Applies);
    }

    [Fact]
    public void ShouldApplyRule_NoGlobs_ReturnsFalse()
    {
        var meta = new RuleMetadata();
        var result = RuleMatcher.ShouldApplyRule(meta, "/foo/bar.ts", "/foo");
        Assert.False(result.Applies);
    }

    [Fact]
    public void CreateContentHash_Deterministic()
    {
        var hash1 = RuleMatcher.CreateContentHash("hello");
        var hash2 = RuleMatcher.CreateContentHash("hello");
        Assert.Equal(hash1, hash2);
        Assert.Equal(16, hash1.Length);
    }

    [Fact]
    public void CreateContentHash_DifferentContent_DifferentHash()
    {
        var hash1 = RuleMatcher.CreateContentHash("hello");
        var hash2 = RuleMatcher.CreateContentHash("world");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void IsDuplicateByRealPath_WorksWithSet()
    {
        var cache = new HashSet<string> { "/real/path" };
        Assert.True(RuleMatcher.IsDuplicateByRealPath("/real/path", cache));
        Assert.False(RuleMatcher.IsDuplicateByRealPath("/other/path", cache));
    }
}

public class RuleDistanceTests
{
    [Fact]
    public void CalculateDistance_NullProjectRoot_ReturnsGlobalDistance()
    {
        Assert.Equal(RuleConstants.GlobalDistance, RuleDistance.CalculateDistance("/a/rule.md", "/a/file.ts", null));
    }

    [Fact]
    public void CalculateDistance_SameDirectory_ReturnsZero()
    {
        Assert.Equal(0, RuleDistance.CalculateDistance("/project/src/rule.md", "/project/src/file.ts", "/project"));
    }

    [Fact]
    public void CalculateDistance_DifferentDirectory_ReturnsPositive()
    {
        var dist = RuleDistance.CalculateDistance("/project/src/rule.md", "/project/src/sub/file.ts", "/project");
        Assert.True(dist > 0);
    }
}

public class RuleConstantsTests
{
    [Fact]
    public void SourcePriority_ContainsAllSources()
    {
        Assert.Equal(10, RuleConstants.SourcePriority.Count);
        Assert.Equal(0, RuleConstants.SourcePriority[".lfe/rules"]);
        Assert.Equal(100, RuleConstants.SourcePriority["~/.lfe/rules"]);
    }

    [Fact]
    public void ExcludedDirs_ContainsExpectedEntries()
    {
        Assert.Contains("node_modules", RuleConstants.ExcludedDirs);
        Assert.Contains(".git", RuleConstants.ExcludedDirs);
    }
}

public class RuleOrderingTests
{
    [Fact]
    public void SortCandidates_ProjectBeforeGlobal()
    {
        var candidates = new List<RuleFileCandidate>
        {
            new("/home/user/.lfe/rules/global.md", "/home/user/.lfe/rules/global.md", true, RuleConstants.GlobalDistance, ".lfe/rules/global.md", "~/.lfe/rules"),
            new("/project/.lfe/rules/local.md", "/project/.lfe/rules/local.md", false, 0, ".lfe/rules/local.md", ".lfe/rules"),
        };

        var sorted = RuleOrdering.SortCandidates(candidates);
        Assert.Equal("/project/.lfe/rules/local.md", sorted[0].Path);
        Assert.Equal("/home/user/.lfe/rules/global.md", sorted[1].Path);
    }

    [Fact]
    public void SortCandidates_CloserDistanceFirst()
    {
        var candidates = new List<RuleFileCandidate>
        {
            new("/project/src/sub/rule.md", "/project/src/sub/rule.md", false, 2, "src/sub/rule.md", ".lfe/rules"),
            new("/project/src/rule.md", "/project/src/rule.md", false, 0, "src/rule.md", ".lfe/rules"),
        };

        var sorted = RuleOrdering.SortCandidates(candidates);
        Assert.Equal("/project/src/rule.md", sorted[0].Path);
    }
}

public class RuleCacheTests
{
    [Fact]
    public void CreateRuleScanCache_StoresAndRetrieves()
    {
        var cache = RuleCache.CreateRuleScanCache();
        var candidates = new List<RuleFileCandidate>
        {
            new("/a.md", "/a.md", false, 0, "a.md", ".lfe/rules"),
        };

        cache.Set("key", candidates);
        var result = cache.Get("key");
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public void CreateRuleScanCache_Stats()
    {
        var cache = RuleCache.CreateRuleScanCache();
        cache.Set("k", []);
        var stats = cache.Stats();
        Assert.Equal(1, stats.CandidateEntries);
        Assert.Equal(0, stats.DirectoryEntries);
    }

    [Fact]
    public void CreateRuleScanCache_Clear()
    {
        var cache = RuleCache.CreateRuleScanCache();
        cache.Set("k", []);
        cache.Clear();
        Assert.Null(cache.Get("k"));
    }

    [Fact]
    public void CreateAgentsMdCache_StoresAndRetrieves()
    {
        var cache = RuleCache.CreateAgentsMdCache();
        cache.Set("key", ["a.md"]);
        var result = cache.Get("key");
        Assert.NotNull(result);
        Assert.Single(result);
    }
}
