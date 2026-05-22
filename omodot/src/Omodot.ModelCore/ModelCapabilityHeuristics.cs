using System.Text.RegularExpressions;

namespace Omodot.ModelCore;

public static class ModelCapabilityHeuristics
{
    public static readonly HeuristicModelFamilyDefinition[] Registry =
    [
        new("claude-opus", Pattern: new Regex(@"claude(?:-\d+(?:-\d+)*)?-opus"), Variants: ["low", "medium", "high", "max"], SupportsThinking: true),
        new("claude-non-opus", Includes: ["claude"], Variants: ["low", "medium", "high"], SupportsThinking: true),
        new("openai-reasoning", Pattern: new Regex(@"(?:^|\/)o\d(?:$|-)"), Variants: ["low", "medium", "high"], ReasoningEfforts: ["none", "minimal", "low", "medium", "high"]),
        new("gpt-5", Includes: ["gpt-5"], Variants: ["low", "medium", "high", "xhigh"], ReasoningEfforts: ["none", "minimal", "low", "medium", "high", "xhigh", "max"]),
        new("gpt-legacy", Includes: ["gpt"], Variants: ["low", "medium", "high"]),
        new("gemini", Includes: ["gemini"], Variants: ["low", "medium", "high"]),
        new("grok", Includes: ["grok"], Variants: ["low", "medium", "high"], ReasoningEfforts: ["low", "medium", "high"]),
        new("kimi-thinking", Includes: ["kimi-thinking", "k2-thinking", "k2-think"], Pattern: new Regex(@"(?:kimi|k2).*-(?:thinking|think)"), Variants: ["low", "medium", "high"], SupportsThinking: true),
        new("kimi", Includes: ["kimi", "k2"], Variants: ["low", "medium", "high"], SupportsThinking: false),
        new("glm", Includes: ["glm"], Variants: ["low", "medium", "high"]),
        new("minimax", Includes: ["minimax"], Variants: ["low", "medium", "high"], SupportsThinking: false),
        new("deepseek", Includes: ["deepseek"], Variants: ["low", "medium", "high"], ReasoningEfforts: ["high", "max"], ReasoningEffortAliases: new() { ["low"] = "high", ["medium"] = "high", ["xhigh"] = "max" }),
        new("mistral", Includes: ["mistral", "codestral"], Variants: ["low", "medium", "high"]),
        new("llama", Includes: ["llama"], Variants: ["low", "medium", "high"]),
    ];

    public static HeuristicModelFamilyDefinition? DetectHeuristicModelFamily(string modelID)
    {
        var normalized = modelID.Trim().ToLowerInvariant();
        foreach (var def in Registry)
        {
            if (def.Pattern?.IsMatch(normalized) == true) return def;
            if (def.Includes?.Any(inc => normalized.Contains(inc)) == true) return def;
        }
        return null;
    }
}
