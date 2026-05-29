using System.IO;

namespace Lfe.TeamModeCore;

public static class TeamModeCorePaths
{
    public static string ResolveBaseDir(TeamModeCorePathConfig config)
    {
        return config.BaseDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".omo");
    }

    private static string GetTeamDirectory(string baseDir, string teamName, string scope, string? projectRoot = null)
    {
        return string.Equals(scope, "project", StringComparison.Ordinal)
            ? Path.Combine(projectRoot ?? string.Empty, ".omo", "teams", teamName)
            : Path.Combine(baseDir, "teams", teamName);
    }

    public static string GetTeamSpecPath(string baseDir, string teamName, string scope, string? projectRoot = null)
    {
        return Path.Combine(GetTeamDirectory(baseDir, teamName, scope, projectRoot), "config.json");
    }

    public static string GetRuntimeStateDir(string baseDir, string teamRunId) => Path.Combine(baseDir, "runtime", teamRunId);

    public static string GetInboxDir(string baseDir, string teamRunId, string memberName) => Path.Combine(baseDir, "runtime", teamRunId, "inboxes", memberName);

    public static string GetTasksDir(string baseDir, string teamRunId) => Path.Combine(baseDir, "runtime", teamRunId, "tasks");

    public static string GetWorktreeDir(string baseDir, string teamRunId, string memberName) => Path.Combine(baseDir, "worktrees", teamRunId, memberName);

    public static List<TeamSpecEntry> MergeDiscoveredTeamSpecs(List<TeamSpecEntry> projectTeamSpecs, List<TeamSpecEntry> userTeamSpecs)
    {
        var discoveredTeamSpecs = new List<TeamSpecEntry>(projectTeamSpecs);
        var projectTeamNames = new HashSet<string>(projectTeamSpecs.Select(entry => entry.Name), StringComparer.Ordinal);

        foreach (var userTeamSpec in userTeamSpecs)
        {
            if (!projectTeamNames.Contains(userTeamSpec.Name))
            {
                discoveredTeamSpecs.Add(userTeamSpec);
            }
        }

        return discoveredTeamSpecs;
    }
}
