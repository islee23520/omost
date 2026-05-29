using System.Text.RegularExpressions;

namespace Lfe.BoulderState.Storage;

public static partial class PlanProgressHelper
{
    private enum ProgressSection
    {
        other,
        todo,
        finalwave,
    }

    [GeneratedRegex(@"^##\s+TODOs\b", RegexOptions.IgnoreCase)]
    private static partial Regex TodoHeadingPattern();

    [GeneratedRegex(@"^##\s+Final Verification Wave\b", RegexOptions.IgnoreCase)]
    private static partial Regex FinalVerificationHeadingPattern();

    [GeneratedRegex(@"^##\s+")]
    private static partial Regex SecondLevelHeadingPattern();

    [GeneratedRegex(@"^(\s*)[-*]\s*\[\s*\]\s*(.+)$")]
    private static partial Regex UncheckedCheckboxPattern();

    [GeneratedRegex(@"^(\s*)[-*]\s*\[[xX]\]\s*(.+)$")]
    private static partial Regex CheckedCheckboxPattern();

    [GeneratedRegex(@"^\d+\.\s+")]
    private static partial Regex TodoTaskPattern();

    [GeneratedRegex(@"^F\d+\.\s+", RegexOptions.IgnoreCase)]
    private static partial Regex FinalWaveTaskPattern();

    [GeneratedRegex(@"^[-*]\s*\[\s*\]", RegexOptions.Multiline)]
    private static partial Regex SimpleUncheckedPattern();

    [GeneratedRegex(@"^[-*]\s*\[[xX]\]", RegexOptions.Multiline)]
    private static partial Regex SimpleCheckedPattern();

    public static List<string> FindPrometheusPlans(string directory)
    {
        var plansDir = Path.Combine(directory, Constants.PROMETHEUS_PLANS_DIR.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(plansDir))
        {
            return [];
        }

        try
        {
            return Directory
                .EnumerateFiles(plansDir, "*.md", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => File.GetLastWriteTimeUtc(file))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static string GetPlanName(string planPath)
    {
        return Path.GetFileNameWithoutExtension(planPath);
    }

    public static PlanProgress GetPlanProgress(string planPath)
    {
        if (!File.Exists(planPath))
        {
            return new PlanProgress { total = 0, completed = 0, isComplete = false };
        }

        try
        {
            var content = File.ReadAllText(planPath);
            var lines = Regex.Split(content, "\\r?\\n");
            var hasStructuredSections = lines.Any(line => TodoHeadingPattern().IsMatch(line) || FinalVerificationHeadingPattern().IsMatch(line));
            return hasStructuredSections ? GetStructuredPlanProgress(lines) : GetSimplePlanProgress(content);
        }
        catch
        {
            return new PlanProgress { total = 0, completed = 0, isComplete = false };
        }
    }

    private static PlanProgress GetStructuredPlanProgress(IEnumerable<string> lines)
    {
        var section = ProgressSection.other;
        var total = 0;
        var completed = 0;

        foreach (var line in lines)
        {
            if (SecondLevelHeadingPattern().IsMatch(line))
            {
                section = TodoHeadingPattern().IsMatch(line)
                    ? ProgressSection.todo
                    : FinalVerificationHeadingPattern().IsMatch(line)
                        ? ProgressSection.finalwave
                        : ProgressSection.other;
                continue;
            }

            if (section is not (ProgressSection.todo or ProgressSection.finalwave))
            {
                continue;
            }

            var checkedMatch = CheckedCheckboxPattern().Match(line);
            var uncheckedMatch = checkedMatch.Success ? null : UncheckedCheckboxPattern().Match(line);
            var match = checkedMatch.Success ? checkedMatch : uncheckedMatch;
            if (match is null || !match.Success || match.Groups[1].Length > 0)
            {
                continue;
            }

            var taskBody = match.Groups[2].Value.Trim();
            var labelPattern = section == ProgressSection.todo ? TodoTaskPattern() : FinalWaveTaskPattern();
            if (!labelPattern.IsMatch(taskBody))
            {
                continue;
            }

            total += 1;
            if (checkedMatch.Success)
            {
                completed += 1;
            }
        }

        return new PlanProgress { total = total, completed = completed, isComplete = total > 0 && completed == total };
    }

    private static PlanProgress GetSimplePlanProgress(string content)
    {
        var uncheckedMatches = SimpleUncheckedPattern().Matches(content).Count;
        var checkedMatches = SimpleCheckedPattern().Matches(content).Count;
        var total = uncheckedMatches + checkedMatches;
        return new PlanProgress { total = total, completed = checkedMatches, isComplete = total > 0 && checkedMatches == total };
    }
}
