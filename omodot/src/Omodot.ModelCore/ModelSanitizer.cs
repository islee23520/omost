namespace Omodot.ModelCore;

public static class ModelSanitizer
{
    public static string? SanitizeModelField(object? model, CommandSource source = CommandSource.ClaudeCode)
    {
        if (source == CommandSource.ClaudeCode) return null;
        if (model is string s && s.Trim().Length > 0) return s.Trim();
        return null;
    }
}
