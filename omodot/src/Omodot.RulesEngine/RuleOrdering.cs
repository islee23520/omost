namespace Omodot.RulesEngine;

public static class RuleOrdering
{
    public static List<RuleFileCandidate> SortCandidates(IReadOnlyList<RuleFileCandidate> candidates)
    {
        return candidates
            .Select((c, i) => (Candidate: c, Index: i))
            .OrderBy(x => x.Candidate.IsGlobal ? 1 : 0)
            .ThenBy(x => x.Candidate.Distance)
            .ThenBy(x => RuleConstants.SourcePriority.GetValueOrDefault(x.Candidate.Source, int.MaxValue))
            .ThenBy(x => x.Candidate.RelativePath, StringComparer.Ordinal)
            .ThenBy(x => x.Candidate.RealPath, StringComparer.Ordinal)
            .ThenBy(x => x.Index)
            .Select(x => x.Candidate)
            .ToList();
    }
}
