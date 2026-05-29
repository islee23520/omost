namespace Lfe.ModelCore;

public enum CommandSource { ClaudeCode, Opencode }

public sealed record FallbackEntry(
    string[] Providers,
    string Model,
    string? Variant = null,
    string? ReasoningEffort = null,
    double? Temperature = null,
    double? TopP = null,
    int? MaxTokens = null,
    ThinkingConfig? Thinking = null
);

public sealed record ThinkingConfig(string Type, int? BudgetTokens = null);

public sealed record ModelRequirement(
    FallbackEntry[] FallbackChain,
    string? Variant = null,
    string? RequiresModel = null,
    bool RequiresAnyModel = false,
    string[]? RequiresProvider = null
);

public sealed record ExactAliasRule(string AliasModelID, string RuleID, string CanonicalModelID, string Rationale);
public sealed record PatternAliasRule(string RuleID, string Description, Func<string, bool> Match, Func<string, string> Canonicalize);

public sealed record ModelIDAliasResolution(
    string RequestedModelID,
    string CanonicalModelID,
    string Source,
    string? RuleID = null
);

public sealed record HeuristicModelFamilyDefinition(
    string Family,
    string[]? Includes = null,
    System.Text.RegularExpressions.Regex? Pattern = null,
    string[]? Variants = null,
    string[]? ReasoningEfforts = null,
    Dictionary<string, string>? ReasoningEffortAliases = null,
    bool? SupportsThinking = null
);

public sealed record ModelSettingsCompatibilityChange(
    string Field,
    string From,
    string? To = null,
    string Reason = ""
);

public sealed record ModelSettingsCompatibilityResult(
    string? Variant,
    string? ReasoningEffort,
    double? Temperature,
    double? TopP,
    int? MaxTokens,
    Dictionary<string, object>? Thinking,
    List<ModelSettingsCompatibilityChange> Changes
);
