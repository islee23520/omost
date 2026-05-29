namespace Lfe.AgentOs;

public static class AgentRoleKinds
{
    public const string Orchestrator = "orchestrator";
    public const string Planner = "planner";
    public const string Worker = "worker";
    public const string Reviewer = "reviewer";
}

public static class AgentReviewStances
{
    public const string Positive = "positive";
    public const string Negative = "negative";
}

public static class DecisionVerdictStatuses
{
    public const string Pass = "PASS";
    public const string Review = "REVIEW";
    public const string Reject = "REJECT";
}

public static class AgentReviewSynthesisActions
{
    public const string Proceed = "proceed";
    public const string MitigateBeforeWorker = "mitigate-before-worker";
    public const string Replan = "replan";
    public const string Fail = "fail";
}

public sealed record AgentRoleGraph(IReadOnlyList<AgentRoleDefinition> Roles);

public sealed record AgentRoleDefinition(
    string Id,
    string Kind,
    bool Hidden,
    string? ReviewStance = null,
    IReadOnlyList<string>? Capabilities = null);

public sealed record AgentReviewSignal(
    string Stance,
    bool Passed,
    bool BlockingRisk,
    string Summary,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> RecommendedActions,
    double Confidence);

public sealed record DecisionVerdict(
    string Status,
    int Score,
    bool HardReject,
    IReadOnlyList<string> Reasons);

public sealed record AgentReviewSynthesisResult(
    string Action,
    DecisionVerdict Verdict,
    IReadOnlyList<string> RecommendedActions);

public static class AgentReviewSynthesis
{
    public static AgentReviewSynthesisResult Synthesize(IReadOnlyList<AgentReviewSignal> reviews)
    {
        var positive = reviews.FirstOrDefault(review => review.Stance == AgentReviewStances.Positive);
        var negative = reviews.FirstOrDefault(review => review.Stance == AgentReviewStances.Negative);
        var recommendedActions = reviews.SelectMany(review => review.RecommendedActions).Distinct(StringComparer.Ordinal).ToArray();

        if (positive?.Passed == true && negative?.Passed == true && negative.BlockingRisk)
        {
            return new AgentReviewSynthesisResult(
                AgentReviewSynthesisActions.MitigateBeforeWorker,
                new DecisionVerdict(DecisionVerdictStatuses.Review, 70, false, negative.Risks),
                recommendedActions);
        }

        if (positive?.Passed == true && negative?.Passed == true)
        {
            return new AgentReviewSynthesisResult(
                AgentReviewSynthesisActions.Proceed,
                new DecisionVerdict(DecisionVerdictStatuses.Pass, 90, false, []),
                recommendedActions);
        }

        if (positive?.Passed == false && negative?.Passed == true)
        {
            return new AgentReviewSynthesisResult(
                AgentReviewSynthesisActions.Replan,
                new DecisionVerdict(DecisionVerdictStatuses.Review, 60, false, positive.Risks),
                recommendedActions);
        }

        return new AgentReviewSynthesisResult(
            AgentReviewSynthesisActions.Fail,
            new DecisionVerdict(DecisionVerdictStatuses.Reject, 40, true, reviews.SelectMany(review => review.Risks).ToArray()),
            recommendedActions);
    }
}

public sealed record AgentContinuationState(
    string RunId,
    string PlanId,
    string NextTaskId,
    bool Accepted);

public sealed record SharedSkillReference(string Id, bool Required);

public sealed record LazyCapabilityRequest(string CapabilityId, string Reason);

public sealed record AgentExecutionContracts(
    AgentContinuationState Continuation,
    IReadOnlyList<SharedSkillReference> SharedSkills,
    IReadOnlyList<LazyCapabilityRequest> LazyCapabilities,
    DecisionVerdict Verdict);
