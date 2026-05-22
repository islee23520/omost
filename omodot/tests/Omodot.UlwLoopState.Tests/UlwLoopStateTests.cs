using Omodot.UlwLoopState;
using Xunit;

namespace Omodot.UlwLoopState.Tests;

public class UlwLoopStateTests
{
    [Fact]
    public void Start_WritesState()
    {
        var store = new MemoryUlwLoopStateStore();
        var ctrl = new UlwLoopStateController(store);
        var state = ctrl.Start(new StartUlwLoopOptions("s1", "do work"));
        Assert.True(state.Active);
        Assert.Equal(1, state.Iteration);
        Assert.Equal("s1", state.SessionID);
        Assert.Equal(UlwLoopConstants.DefaultCompletionPromise, state.CompletionPromise);
        Assert.Equal(UlwLoopConstants.DefaultMaxIterations, state.MaxIterations);
    }

    [Fact]
    public void Start_Ultrawork_SetsHigherMax()
    {
        var store = new MemoryUlwLoopStateStore();
        var ctrl = new UlwLoopStateController(store);
        var state = ctrl.Start(new StartUlwLoopOptions("s1", "ulw", Ultrawork: true));
        Assert.Equal(UlwLoopConstants.UltraworkMaxIterations, state.MaxIterations);
        Assert.True(state.Ultrawork);
    }

    [Fact]
    public void Cancel_ReturnsTrue_WhenSessionMatches()
    {
        var store = new MemoryUlwLoopStateStore();
        var ctrl = new UlwLoopStateController(store);
        ctrl.Start(new StartUlwLoopOptions("s1", "work"));
        Assert.True(ctrl.Cancel("s1"));
        Assert.Null(ctrl.GetState());
    }

    [Fact]
    public void Cancel_ReturnsFalse_WhenSessionMismatch()
    {
        var store = new MemoryUlwLoopStateStore();
        var ctrl = new UlwLoopStateController(store);
        ctrl.Start(new StartUlwLoopOptions("s1", "work"));
        Assert.False(ctrl.Cancel("other"));
    }

    [Fact]
    public void IncrementIteration_Increments()
    {
        var store = new MemoryUlwLoopStateStore();
        var ctrl = new UlwLoopStateController(store);
        ctrl.Start(new StartUlwLoopOptions("s1", "work"));
        var next = ctrl.IncrementIteration();
        Assert.NotNull(next);
        Assert.Equal(2, next.Iteration);
    }

    [Fact]
    public void IncrementIteration_WithExpectation_FailsOnMismatch()
    {
        var store = new MemoryUlwLoopStateStore();
        var ctrl = new UlwLoopStateController(store);
        ctrl.Start(new StartUlwLoopOptions("s1", "work"));
        Assert.Null(ctrl.IncrementIteration(new IterationExpectation(99, "s1")));
    }

    [Fact]
    public void SetSessionID_UpdatesSessionID()
    {
        var store = new MemoryUlwLoopStateStore();
        var ctrl = new UlwLoopStateController(store);
        ctrl.Start(new StartUlwLoopOptions("s1", "work"));
        var updated = ctrl.SetSessionID("s1", "s2");
        Assert.NotNull(updated);
        Assert.Equal("s2", updated.SessionID);
    }

    [Fact]
    public void MarkVerificationPending_RequiresUltrawork()
    {
        var store = new MemoryUlwLoopStateStore();
        var ctrl = new UlwLoopStateController(store);
        ctrl.Start(new StartUlwLoopOptions("s1", "work"));
        Assert.Null(ctrl.MarkVerificationPending("s1"));
    }

    [Fact]
    public void MarkVerificationPending_SetsPromise()
    {
        var store = new MemoryUlwLoopStateStore();
        var ctrl = new UlwLoopStateController(store);
        ctrl.Start(new StartUlwLoopOptions("s1", "work", Ultrawork: true));
        var updated = ctrl.MarkVerificationPending("s1");
        Assert.NotNull(updated);
        Assert.Equal(UlwLoopConstants.UltraworkVerificationPromise, updated.CompletionPromise);
        Assert.True(updated.VerificationPending);
    }
}

public class StateSerializerTests
{
    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        var original = new UlwLoopState(true, 3, 100, "DONE", "DONE", "2024-01-01T00:00:00Z", "my prompt", "s1");
        var serialized = StateSerializer.Serialize(original);
        var deserialized = StateSerializer.Deserialize(serialized);
        Assert.NotNull(deserialized);
        Assert.True(deserialized.Active);
        Assert.Equal(3, deserialized.Iteration);
        Assert.Equal("s1", deserialized.SessionID);
        Assert.Equal("my prompt", deserialized.Prompt);
    }

    [Fact]
    public void Deserialize_InvalidYaml_ReturnsNull()
    {
        Assert.Null(StateSerializer.Deserialize("not yaml"));
    }
}
