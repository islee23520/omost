using System.Text.Json;

namespace Lfe.UlwLoopState;

public enum UlwLoopStrategy { Reset, Continue }

public sealed record UlwLoopState(
    bool Active,
    int Iteration,
    int MaxIterations,
    string CompletionPromise,
    string InitialCompletionPromise,
    string StartedAt,
    string Prompt,
    string SessionID,
    int? MessageCountAtStart = null,
    bool? VerificationPending = null,
    string? VerificationAttemptID = null,
    string? VerificationSessionID = null,
    UlwLoopStrategy Strategy = UlwLoopStrategy.Continue,
    bool? Ultrawork = null);

public sealed record StartUlwLoopOptions(
    string SessionID,
    string Prompt,
    int? MaxIterations = null,
    string? CompletionPromise = null,
    int? MessageCountAtStart = null,
    bool? Ultrawork = null,
    UlwLoopStrategy? Strategy = null,
    Func<string>? Now = null);

public sealed record IterationExpectation(int Iteration, string SessionID);

public interface IUlwLoopStateStore
{
    UlwLoopState? Read();
    void Write(UlwLoopState state);
    void Clear();
}

public static class UlwLoopConstants
{
    public const string DefaultCompletionPromise = "DONE";
    public const string UltraworkVerificationPromise = "VERIFIED";
    public const int DefaultMaxIterations = 100;
    public const int UltraworkMaxIterations = 500;
    public const string DefaultStateFile = ".omo/ulw-loop.local.md";
}
