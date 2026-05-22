using System.Text.RegularExpressions;

namespace Omodot.BoulderState;

public static partial class TopLevelTask
{
    private enum PlanSection
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

    [GeneratedRegex(@"^(\d+)\.\s+(.+)$")]
    private static partial Regex TodoTaskPattern();

    [GeneratedRegex(@"^(F\d+)\.\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex FinalWaveTaskPattern();

    public static TopLevelTaskRef? ReadCurrentTopLevelTask(string planPath)
    {
        if (!File.Exists(planPath))
        {
            return null;
        }

        try
        {
            var lines = File.ReadAllLines(planPath);
            var section = PlanSection.other;

            foreach (var line in lines)
            {
                if (SecondLevelHeadingPattern().IsMatch(line))
                {
                    section = TodoHeadingPattern().IsMatch(line)
                        ? PlanSection.todo
                        : FinalVerificationHeadingPattern().IsMatch(line)
                            ? PlanSection.finalwave
                            : PlanSection.other;
                }

                var uncheckedTaskMatch = UncheckedCheckboxPattern().Match(line);
                if (!uncheckedTaskMatch.Success || uncheckedTaskMatch.Groups[1].Length > 0)
                {
                    continue;
                }

                if (section is not (PlanSection.todo or PlanSection.finalwave))
                {
                    continue;
                }

                var taskRef = BuildTaskRef(section, uncheckedTaskMatch.Groups[2].Value.Trim());
                if (taskRef is not null)
                {
                    return taskRef;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static TopLevelTaskRef? BuildTaskRef(PlanSection section, string taskLabel)
    {
        var pattern = section == PlanSection.todo ? TodoTaskPattern() : FinalWaveTaskPattern();
        var match = pattern.Match(taskLabel);
        if (!match.Success)
        {
            return null;
        }

        var rawLabel = match.Groups[1].Value;
        return new TopLevelTaskRef
        {
            key = $"{(section == PlanSection.todo ? "todo" : "final-wave")}:{rawLabel.ToLowerInvariant()}",
            section = section == PlanSection.todo ? "todo" : "final-wave",
            label = rawLabel,
            title = match.Groups[2].Value.Trim(),
        };
    }
}
