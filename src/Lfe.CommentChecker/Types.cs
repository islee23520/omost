namespace Lfe.CommentChecker;

public enum CommentType { Line, Block, Docstring }

public sealed record CommentInfo(string Text, int LineNumber, string FilePath, CommentType CommentType, bool IsDocstring, Dictionary<string, string>? Metadata = null);
public sealed record PendingCall(string FilePath, string? Content, string? OldString, string? NewString, object[]? Edits, string Tool, string SessionID, long Timestamp);
public sealed record FileComments(string FilePath, IReadOnlyList<CommentInfo> Comments);
public sealed record FilterResult(bool ShouldSkip, string? Reason = null);
public sealed record CheckerEdit(string FilePath, string Before, string After);
public sealed record ApplyPatchFileMetadata(string FilePath, string? MovePath, string Before, string After, string? Type = null);
public sealed record ApplyPatchAccumulator(string Operation, string FilePath, string? MovePath, List<string> OldLines, List<string> NewLines);
public sealed record CheckResult(bool HasComments, string Message);

public interface HookInput
{
    string SessionId { get; }
    string ToolName { get; }
    string TranscriptPath { get; }
    string Cwd { get; }
    string HookEventName { get; }
    object ToolInput { get; }
    object? ToolResponse { get; }
}
