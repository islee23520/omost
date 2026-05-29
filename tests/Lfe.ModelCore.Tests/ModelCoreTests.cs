using Lfe.ModelCore;
using Xunit;

namespace Lfe.ModelCore.Tests;

public class ModelSanitizerTests
{
    [Fact]
    public void ClaudeCode_ReturnsNull() => Assert.Null(ModelSanitizer.SanitizeModelField("claude-opus", CommandSource.ClaudeCode));

    [Fact]
    public void Opencode_Trims() => Assert.Equal("openai/gpt-5.4", ModelSanitizer.SanitizeModelField(" openai/gpt-5.4 ", CommandSource.Opencode));

    [Fact]
    public void Opencode_Empty_ReturnsNull() => Assert.Null(ModelSanitizer.SanitizeModelField("   ", CommandSource.Opencode));

    [Fact]
    public void Opencode_NonString_ReturnsNull() => Assert.Null(ModelSanitizer.SanitizeModelField(42, CommandSource.Opencode));
}

public class ModelRequirementsTests
{
    [Fact]
    public void AgentRequirements_HasSisyphus() => Assert.True(ModelRequirements.AgentModelRequirements.ContainsKey("sisyphus"));

    [Fact]
    public void CategoryRequirements_HasQuick() => Assert.True(ModelRequirements.CategoryModelRequirements.ContainsKey("quick"));

    [Fact]
    public void GetBuiltInModelIDs_NotEmpty()
    {
        var ids = ModelRequirements.GetBuiltInRequirementModelIDs();
        Assert.NotEmpty(ids);
        Assert.Contains("claude-opus-4-7", ids);
        Assert.Contains("gpt-5.5", ids);
    }
}

public class ModelCapabilityAliasesTests
{
    [Fact]
    public void Canonical_ReturnsSame()
    {
        var result = ModelCapabilityAliases.ResolveModelIDAlias("gpt-5.5");
        Assert.Equal("canonical", result.Source);
        Assert.Equal("gpt-5.5", result.CanonicalModelID);
    }

    [Fact]
    public void ExactAlias_Resolves()
    {
        var result = ModelCapabilityAliases.ResolveModelIDAlias("claude-opus-4.7");
        Assert.Equal("exact-alias", result.Source);
        Assert.Equal("claude-opus-4-7", result.CanonicalModelID);
    }

    [Fact]
    public void PatternAlias_Resolves()
    {
        var result = ModelCapabilityAliases.ResolveModelIDAlias("claude-opus-4-7-thinking");
        Assert.Equal("pattern-alias", result.Source);
        Assert.Equal("claude-opus-4-7", result.CanonicalModelID);
    }

    [Fact]
    public void ProviderPrefix_Stripped()
    {
        var result = ModelCapabilityAliases.ResolveModelIDAlias("anthropic/claude-opus-4.7");
        Assert.Equal("exact-alias", result.Source);
        Assert.Equal("claude-opus-4-7", result.CanonicalModelID);
    }
}

public class ModelCapabilityHeuristicsTests
{
    [Fact]
    public void Detects_ClaudeOpus()
    {
        var family = ModelCapabilityHeuristics.DetectHeuristicModelFamily("claude-opus-4-7");
        Assert.NotNull(family);
        Assert.Equal("claude-opus", family.Family);
        Assert.True(family.SupportsThinking);
    }

    [Fact]
    public void Detects_GPT5()
    {
        var family = ModelCapabilityHeuristics.DetectHeuristicModelFamily("gpt-5.5");
        Assert.NotNull(family);
        Assert.Equal("gpt-5", family.Family);
    }

    [Fact]
    public void Detects_Gemini()
    {
        var family = ModelCapabilityHeuristics.DetectHeuristicModelFamily("gemini-3.1-pro");
        Assert.NotNull(family);
        Assert.Equal("gemini", family.Family);
    }

    [Fact]
    public void Unknown_ReturnsNull() => Assert.Null(ModelCapabilityHeuristics.DetectHeuristicModelFamily("unknown-model"));
}

public class ModelSettingsCompatibilityTests
{
    [Fact]
    public void Compatible_NoChanges()
    {
        var result = ModelSettingsCompatibility.ResolveCompatibleModelSettings("openai", "gpt-5.5", "medium", null, null, null, null, null);
        Assert.Equal("medium", result.Variant);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void Downgrades_Variant()
    {
        var result = ModelSettingsCompatibility.ResolveCompatibleModelSettings("anthropic", "claude-haiku-4-5", "xhigh", null, null, null, null, null);
        Assert.NotEmpty(result.Changes);
    }

    [Fact]
    public void Strips_UnsupportedTemperature()
    {
        var result = ModelSettingsCompatibility.ResolveCompatibleModelSettings("openai", "gpt-5.5", null, null, 0.7, null, null, null, supportsTemperature: false);
        Assert.Null(result.Temperature);
        Assert.Contains(result.Changes, c => c.Field == "temperature");
    }

    [Fact]
    public void Caps_MaxTokens()
    {
        var result = ModelSettingsCompatibility.ResolveCompatibleModelSettings("openai", "gpt-5.5", null, null, null, null, 100000, null, maxOutputTokens: 32768);
        Assert.Equal(32768, result.MaxTokens);
        Assert.Contains(result.Changes, c => c.Field == "maxTokens");
    }
}

public class ModelNormalizationTests
{
    [Fact]
    public void Normalize_TrimsAndLowers() => Assert.Equal("gpt-5.5", ModelNormalization.NormalizeModelID(" GPT-5.5 "));
}
