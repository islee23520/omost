namespace Lfe.BoulderState.Storage;

public static class PathHelper
{
    public static string GetBoulderFilePath(string directory)
    {
        return Path.Combine(directory, Constants.BOULDER_DIR.Replace('/', Path.DirectorySeparatorChar), Constants.BOULDER_FILE);
    }

    public static string ResolveBoulderPlanPath(string directory, string active_plan, string? worktree_path)
    {
        var absolutePlanPath = ResolveTrackedPath(directory, active_plan);
        if (string.IsNullOrWhiteSpace(worktree_path))
        {
            return absolutePlanPath;
        }

        var absoluteDirectory = Path.GetFullPath(directory);
        var relativePlanPath = Path.GetRelativePath(absoluteDirectory, absolutePlanPath);
        if (relativePlanPath.Length == 0 || relativePlanPath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePlanPath))
        {
            return absolutePlanPath;
        }

        var absoluteWorktreePath = ResolveTrackedPath(directory, worktree_path.Trim());
        var worktreePlanPath = Path.GetFullPath(Path.Combine(absoluteWorktreePath, relativePlanPath));
        return File.Exists(worktreePlanPath) ? worktreePlanPath : absolutePlanPath;
    }

    public static string ResolveBoulderPlanPath(string directory, BoulderState state)
    {
        return ResolveBoulderPlanPath(directory, state.active_plan, state.worktree_path);
    }

    public static string ResolveBoulderPlanPathForWork(string directory, BoulderWorkState work)
    {
        return ResolveBoulderPlanPath(directory, work.active_plan, work.worktree_path);
    }

    private static string ResolveTrackedPath(string baseDirectory, string trackedPath)
    {
        return Path.GetFullPath(Path.IsPathRooted(trackedPath) ? trackedPath : Path.Combine(baseDirectory, trackedPath));
    }
}
