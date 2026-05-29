using System.Diagnostics;

namespace Lfe.GitWorktree;

public static class CollectGitDiffStats
{
    public static List<GitFileStat> Collect(string directory)
    {
        try
        {
            var diffOutput = RunGit(directory, ["diff", "--numstat", "HEAD"]);
            var statusOutput = RunGit(directory, ["status", "--porcelain"]);
            var untrackedOutput = RunGit(directory, ["ls-files", "--others", "--exclude-standard"]);
            var statusMap = ParseStatusPorcelain.Parse(statusOutput);

            var untrackedNumstat = string.IsNullOrEmpty(untrackedOutput) ? "" :
                string.Join("\n", untrackedOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(fp =>
                {
                    try { var content = File.ReadAllText(Path.Join(directory, fp)); var count = content.Split('\n').Length - (content.EndsWith('\n') ? 1 : 0); return $"{count}\t0\t{fp}"; }
                    catch { return $"0\t0\t{fp}"; }
                }));

            var combined = string.Join("\n", new[] { diffOutput, untrackedNumstat }.Where(s => !string.IsNullOrEmpty(s))).Trim();
            if (string.IsNullOrEmpty(combined)) return [];
            return ParseDiffNumstat.Parse(combined, statusMap);
        }
        catch { return []; }
    }

    private static string RunGit(string cwd, string[] args)
    {
        var psi = new ProcessStartInfo("git", string.Join(" ", args)) { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        using var proc = Process.Start(psi);
        return proc?.StandardOutput.ReadToEnd().TrimEnd() ?? "";
    }
}
