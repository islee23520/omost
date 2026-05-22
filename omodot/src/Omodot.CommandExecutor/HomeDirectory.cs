namespace Omodot.CommandExecutor;

public static class HomeDirectory
{
    public static string GetHomeDirectory()
    {
        return Environment.GetEnvironmentVariable("HOME")
            ?? Environment.GetEnvironmentVariable("USERPROFILE")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
