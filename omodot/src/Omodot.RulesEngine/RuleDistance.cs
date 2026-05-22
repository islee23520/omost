namespace Omodot.RulesEngine;

public static class RuleDistance
{
    public static int CalculateDistance(string rulePath, string currentFile, string? projectRoot)
    {
        if (projectRoot is null) return RuleConstants.GlobalDistance;
        try
        {
            var ruleRelative = System.IO.Path.GetRelativePath(projectRoot, System.IO.Path.GetDirectoryName(rulePath)!);
            var currentRelative = System.IO.Path.GetRelativePath(projectRoot, System.IO.Path.GetDirectoryName(currentFile)!);
            if (ruleRelative.StartsWith("..") || currentRelative.StartsWith(".."))
                return RuleConstants.GlobalDistance;

            var ruleParts = ToParts(ruleRelative);
            var currentParts = ToParts(currentRelative);
            var shared = 0;
            for (int i = 0; i < Math.Min(ruleParts.Length, currentParts.Length); i++)
            {
                if (ruleParts[i] != currentParts[i]) break;
                shared++;
            }
            return currentParts.Length - shared;
        }
        catch { return RuleConstants.GlobalDistance; }
    }

    private static string[] ToParts(string path) => path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
}
