namespace Lfe.Hooks;

using System.Text.RegularExpressions;

public static partial class ModelAgentGuard
{
    #region Think Keyword Detection

    private static readonly string[] ThinkKeywords =
    [
        "생각", "검토", "제대로", "思考", "考虑", "考慮", "考え", "熟考",
        "सोच", "विचार", "تفكير", "تأمل", "চিন্তা", "ভাবনা",
        "думать", "думай", "размышлять", "размышляй",
        "pensar", "pense", "refletir", "reflita", "piensa", "reflexionar", "reflexiona",
        "penser", "pense", "réfléchir", "réfléchis",
        "denken", "denk", "nachdenken",
        "suy nghĩ", "cân nhắc", "düşün", "düşünmek",
        "pensare", "pensa", "riflettere", "rifletti",
        "คิด", "พิจารณา", "myśl", "myśleć", "zastanów", "nadenken",
        "berpikir", "pikir", "pertimbangkan",
        "думати", "думай", "роздумувати",
        "σκέψου", "σκέφτομαι", "myslet", "mysli", "přemýšlet",
        "gândește", "gândi", "reflectă",
        "tänka", "tänk", "fundera",
        "gondolkodj", "gondolkodni", "ajattele", "ajatella", "pohdi",
        "tænk", "tænke", "overvej", "tenk", "tenke", "gruble",
        "חשוב", "לחשוב", "להרהר", "fikir", "berfikir",
    ];

    [GeneratedRegex(@"```[\s\S]*?```")]
    private static partial Regex CodeBlockPattern();

    [GeneratedRegex(@"`[^`]+`")]
    private static partial Regex InlineCodePattern();

    [GeneratedRegex(@"\b(?:ultrathink|think)\b", RegexOptions.IgnoreCase)]
    private static partial Regex UltrathinkPattern();

    public static bool DetectThinkKeyword(string text)
    {
        var withoutCode = CodeBlockPattern().Replace(text, "");
        withoutCode = InlineCodePattern().Replace(withoutCode, "");
        if (UltrathinkPattern().IsMatch(withoutCode)) return true;
        var lower = withoutCode.ToLowerInvariant();
        return ThinkKeywords.Any(kw => lower.Contains(kw.ToLowerInvariant()));
    }

    public static bool IsAlreadyHighReasoningVariant(string modelID)
    {
        var normalized = Regex.Replace(modelID, @"(gpt-\d+)\.(\d+)", "$1-$2");
        var basePath = normalized.Contains('/') ? normalized.Split('/').Last() : normalized;
        return basePath.EndsWith("-high");
    }

    public static (ThinkModeState State, string? Variant) ResolveThinkMode(
        List<(string Type, string? Text)> parts, ModelRef? model, Dictionary<string, object> message)
    {
        var promptText = string.Join("", parts
            .Where(p => p.Type == "text")
            .Select(p => p.Text ?? ""));

        var state = new ThinkModeState(false, false, false);
        if (!DetectThinkKeyword(promptText)) return (state, null);

        state = state with { Requested = true };
        if (message.TryGetValue("variant", out var v) && v is string || model == null) return (state, null);

        state = state with { ProviderID = model.ProviderID, ModelID = model.ModelID };
        if (IsAlreadyHighReasoningVariant(model.ModelID)) return (state, null);

        state = state with { VariantSet = true };
        return (state, "high");
    }

    #endregion

    #region Model Agent Guard

    public static ModelAgentGuardDecision ResolveModelAgentGuard(
        string? agent, ModelRef? model, ModelAgentGuardOptions? options = null)
    {
        options ??= ModelAgentGuardOptions.Empty;
        var agentKey = GetAgentConfigKey(agent ?? "");
        var modelID = model?.ModelID;

        if (agentKey == "sisyphus" && modelID is not null && IsGptNativeSisyphusModel(modelID))
        {
            return new ModelAgentGuardDecision(Variant: GetNativeSisyphusGptVariant(model!));
        }

        if (agentKey == "sisyphus" && modelID is not null && IsGptModel(modelID))
        {
            return new ModelAgentGuardDecision(
                Agent: "hephaestus", OutputAgent: "hephaestus", SessionAgent: "hephaestus",
                Toast: new ToastInfo("NEVER Use Sisyphus with GPT",
                    "Sisyphus works best with Claude Opus, and works fine with Kimi/GLM models.\n" +
                    "Do NOT use Sisyphus with GPT (except GPT-5.4 and GPT-5.5 which have specialized support).\n" +
                    "For other GPT models, always use Hephaestus.", "error"));
        }

        if (agentKey == "hephaestus" && modelID is not null && !IsGptModel(modelID))
        {
            var allowed = options.AllowHephaestusNonGptModel;
            return new ModelAgentGuardDecision(
                Agent: allowed ? null : "sisyphus",
                OutputAgent: allowed ? null : "sisyphus",
                SessionAgent: allowed ? null : "sisyphus",
                Toast: new ToastInfo("NEVER Use Hephaestus with Non-GPT",
                    "Hephaestus is designed exclusively for GPT models.\n" +
                    "Hephaestus is trash without GPT.\n" +
                    "For Claude/Kimi/GLM models, always use Sisyphus.",
                    allowed ? "warning" : "error"));
        }

        return new ModelAgentGuardDecision();
    }

    public static bool IsGptModel(string model) =>
        ExtractModelName(model).ToLowerInvariant().Contains("gpt");

    [GeneratedRegex(@"gpt-5[.-](?:[4-9]|\d{2,})", RegexOptions.IgnoreCase)]
    private static partial Regex GptNativePattern();

    public static bool IsGptNativeSisyphusModel(string model) =>
        GptNativePattern().IsMatch(ExtractModelName(model).ToLowerInvariant());

    #endregion

    #region Anthropic Effort

    [GeneratedRegex(@"claude-.*opus", RegexOptions.IgnoreCase)]
    private static partial Regex AnthropicOpusPattern();

    [GeneratedRegex(@"claude-.*haiku", RegexOptions.IgnoreCase)]
    private static partial Regex AnthropicEffortUnsupportedPattern();

    private static readonly HashSet<string> AnthropicInternalSkipAgents = ["title", "summary", "compaction"];

    private static readonly HashSet<string> ClaudeProviders = ["anthropic", "google-vertex-anthropic", "opencode"];

    public static bool IsClaudeProvider(string providerID, string modelID) =>
        ClaudeProviders.Contains(providerID) ||
        (providerID == "github-copilot" && modelID.ToLowerInvariant().Contains("claude"));

    public static bool IsOpusModel(string modelID) =>
        AnthropicOpusPattern().IsMatch(NormalizeAnthropicModelID(modelID));

    public static bool IsEffortUnsupportedModel(string modelID) =>
        AnthropicEffortUnsupportedPattern().IsMatch(NormalizeAnthropicModelID(modelID));

    public static bool ShouldSkipForInternalAgent(string? agentName) =>
        agentName is not null && AnthropicInternalSkipAgents.Contains(agentName.Trim().ToLowerInvariant());

    public static AnthropicEffortDecision ResolveAnthropicEffort(
        AnthropicEffortInput input, AnthropicEffortOutput output, Func<string, bool>? isConstrainedProvider = null)
    {
        var providerID = input.Model?.ProviderID;
        var modelID = input.Model?.ModelID;

        if (providerID is null || modelID is null || !IsClaudeProvider(providerID, modelID))
            return new AnthropicEffortDecision(Reason: AnthropicEffortReason.NotClaude);

        if (IsEffortUnsupportedModel(modelID))
            return new AnthropicEffortDecision(Reason: AnthropicEffortReason.UnsupportedModel);

        if (ShouldSkipForInternalAgent(input.Agent?.Name))
            return new AnthropicEffortDecision(Reason: AnthropicEffortReason.InternalAgent);

        var opus = IsOpusModel(modelID);
        var constrained = providerID == "github-copilot" || isConstrainedProvider?.Invoke(providerID) == true;

        if (output.Options.TryGetValue("effort", out var effortObj) && effortObj is not null)
        {
            var effortStr = effortObj.ToString();
            if (effortStr == "max" && constrained)
                return new AnthropicEffortDecision(Effort: "high", Variant: "high", Reason: AnthropicEffortReason.ClampedExisting);
            return new AnthropicEffortDecision(Effort: effortStr, Reason: AnthropicEffortReason.ExistingEffort);
        }

        if (input.Message?.Variant != "max")
            return new AnthropicEffortDecision(Reason: AnthropicEffortReason.VariantNotMax);

        var finalEffort = opus && !constrained ? "max" : "high";
        return new AnthropicEffortDecision(
            Effort: finalEffort,
            Variant: finalEffort == "max" ? null : finalEffort,
            Reason: finalEffort == "max" ? AnthropicEffortReason.Injected : AnthropicEffortReason.ClampedVariant);
    }

    #endregion

    #region Helpers

    public static string ExtractModelName(string model) =>
        model.Contains('/') ? model.Split('/').Last() : model;

    private static string NormalizeAnthropicModelID(string modelID) =>
        ExtractModelName(modelID).ToLowerInvariant().Replace(".", "-");

    private static string GetAgentConfigKey(string agent) =>
        agent.Trim().ToLowerInvariant().Replace("_", "-").Replace(" ", "-");

    private static string? GetNativeSisyphusGptVariant(ModelRef model)
    {
        if (model.ModelID == "gpt-5.5" || model.ModelID.EndsWith("/gpt-5.5")) return "medium";
        return null;
    }

    #endregion
}
