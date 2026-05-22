namespace Omodot.ModelCore;

public static class ModelSettingsCompatibility
{
    private static readonly string[] VariantLadder = ["low", "medium", "high", "xhigh", "max"];
    private static readonly string[] ReasoningLadder = ["none", "minimal", "low", "medium", "high", "xhigh", "max"];

    public static ModelSettingsCompatibilityResult ResolveCompatibleModelSettings(
        string providerID, string modelID,
        string? desiredVariant, string? desiredReasoningEffort,
        double? desiredTemperature, double? desiredTopP,
        int? desiredMaxTokens, Dictionary<string, object>? desiredThinking,
        string[]? capabilityVariants = null, string[]? capabilityReasoningEfforts = null,
        bool? supportsTemperature = null, bool? supportsTopP = null,
        int? maxOutputTokens = null, bool? supportsThinking = null)
    {
        var family = ModelCapabilityHeuristics.DetectHeuristicModelFamily(modelID);
        var familyKnown = family is not null;
        var changes = new List<ModelSettingsCompatibilityChange>();

        string? variant = desiredVariant;
        if (variant is not null)
        {
            var normalized = variant.ToLowerInvariant();
            var resolved = ResolveField(normalized, family?.Variants, VariantLadder, familyKnown, capabilityVariants);
            if (resolved.Value != normalized && resolved.Reason is not null)
                changes.Add(new("variant", variant, resolved.Value, resolved.Reason));
            variant = resolved.Value;
        }

        string? reasoningEffort = desiredReasoningEffort;
        if (reasoningEffort is not null)
        {
            var normalized = reasoningEffort.ToLowerInvariant();
            var resolved = ResolveField(normalized, family?.ReasoningEfforts, ReasoningLadder, familyKnown, capabilityReasoningEfforts, family?.ReasoningEffortAliases);
            if (resolved.Value != normalized && resolved.Reason is not null)
                changes.Add(new("reasoningEffort", reasoningEffort, resolved.Value, resolved.Reason));
            reasoningEffort = resolved.Value;
        }

        double? temperature = desiredTemperature;
        if (temperature is not null && supportsTemperature == false)
        {
            changes.Add(new("temperature", temperature.Value.ToString(), null, "unsupported-by-model-metadata"));
            temperature = null;
        }

        double? topP = desiredTopP;
        if (topP is not null && supportsTopP == false)
        {
            changes.Add(new("topP", topP.Value.ToString(), null, "unsupported-by-model-metadata"));
            topP = null;
        }

        int? maxTokens = desiredMaxTokens;
        if (maxTokens is not null && maxTokens <= 0) maxTokens = null;
        if (maxTokens is not null && maxOutputTokens is not null && maxOutputTokens > 0 && maxTokens > maxOutputTokens)
        {
            changes.Add(new("maxTokens", maxTokens.Value.ToString(), maxOutputTokens.Value.ToString(), "max-output-limit"));
            maxTokens = maxOutputTokens;
        }

        var thinking = desiredThinking;
        if (thinking is not null && supportsThinking == false)
        {
            changes.Add(new("thinking", System.Text.Json.JsonSerializer.Serialize(thinking), null, "unsupported-by-model-metadata"));
            thinking = null;
        }

        return new(variant, reasoningEffort, temperature, topP, maxTokens, thinking, changes);
    }

    private static (string? Value, string? Reason) ResolveField(
        string normalized, string[]? familyCaps, string[] ladder, bool familyKnown,
        string[]? metadataOverride, Dictionary<string, string>? aliases = null)
    {
        if (aliases?.TryGetValue(normalized, out var aliased) == true &&
            (metadataOverride?.Contains(aliased) == true || familyCaps?.Contains(aliased) == true))
            return (aliased, "unsupported-by-model-family");

        if (metadataOverride is not null)
        {
            if (metadataOverride.Contains(normalized)) return (normalized, null);
            return (Downgrade(normalized, metadataOverride, ladder), "unsupported-by-model-metadata");
        }
        if (familyCaps is not null)
        {
            if (familyCaps.Contains(normalized)) return (normalized, null);
            return (Downgrade(normalized, familyCaps, ladder), "unsupported-by-model-family");
        }
        if (familyKnown) return (null, "unsupported-by-model-family");
        return (null, "unknown-model-family");
    }

    private static string? Downgrade(string value, string[] allowed, string[] ladder)
    {
        var idx = Array.IndexOf(ladder, value);
        if (idx < 0) return null;
        for (var i = idx; i >= 0; i--)
            if (allowed.Contains(ladder[i])) return ladder[i];
        return null;
    }
}
