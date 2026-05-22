using System.Runtime.InteropServices;

namespace Omodot.Utils;

public static class BunWhichShim
{
    public static string? BunWhich(string commandName) => Which(commandName);

    public static string? Which(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName) || IsUnsafeCommandName(commandName))
        {
            return null;
        }

        var pathValue = ResolvePathValue();
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var pathEntry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidateName in GetCandidateNames(commandName))
            {
                var candidatePath = Path.Combine(pathEntry, candidateName);
                if (IsExecutable(candidatePath))
                {
                    return candidatePath;
                }
            }
        }

        return null;
    }

    private static bool IsUnsafeCommandName(string commandName)
    {
        return commandName.Contains('/')
            || commandName.Contains('\\')
            || commandName == "."
            || commandName == ".."
            || commandName.Contains("..", StringComparison.Ordinal)
            || commandName.Contains('\0')
            || (commandName.Length >= 2 && char.IsLetter(commandName[0]) && commandName[1] == ':');
    }

    private static string? ResolvePathValue()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Environment.GetEnvironmentVariable("Path") ?? Environment.GetEnvironmentVariable("PATH")
            : Environment.GetEnvironmentVariable("PATH");
    }

    private static IReadOnlyList<string> GetCandidateNames(string commandName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return [commandName];
        }

        return [commandName, $"{commandName}.exe", $"{commandName}.cmd", $"{commandName}.bat", $"{commandName}.com"];
    }

    private static bool IsExecutable(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return true;
        }

        try
        {
            var mode = File.GetUnixFileMode(filePath);
            return mode.HasFlag(UnixFileMode.UserExecute)
                || mode.HasFlag(UnixFileMode.GroupExecute)
                || mode.HasFlag(UnixFileMode.OtherExecute);
        }
        catch
        {
            return false;
        }
    }
}
