using Lfe.Protocol.Types;

namespace Lfe.Protocol.Execution;

/// <summary>
/// Defines the contract for executing LFE runs.
/// </summary>
public interface ILfeRunExecutor
{
    /// <summary>
    /// Dispatches a run request asynchronously.
    /// </summary>
    /// <param name="request">The run dispatch request parameters.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the run acceptance result.</returns>
    Task<LfeRunAccepted> DispatchAsync(RunDispatchRequestParams request, CancellationToken ct);

    /// <summary>
    /// Cancels an active run asynchronously.
    /// </summary>
    /// <param name="runId">The identifier of the run to cancel.</param>
    /// <param name="reason">The optional reason for cancellation.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the cancellation result.</returns>
    Task<LfeCancelResult> CancelAsync(string runId, string? reason, CancellationToken ct);
}

/// <summary>
/// Represents the result of a run dispatch request being accepted.
/// </summary>
public sealed record LfeRunAccepted
{
    /// <summary>
    /// Gets the identifier of the run.
    /// </summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the run was accepted.
    /// </summary>
    public bool Accepted { get; init; }
}

/// <summary>
/// Represents the result of a run cancellation request.
/// </summary>
public sealed record LfeCancelResult
{
    /// <summary>
    /// Gets the identifier of the run.
    /// </summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the status of the run after cancellation.
    /// </summary>
    public string Status { get; init; } = LfeRunStatusValues.Cancelled;
}
