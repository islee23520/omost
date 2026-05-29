using Lfe.BackgroundAgent;

namespace Lfe.BackgroundAgent.Tests;

public sealed class BackgroundAgentTests
{
    [Fact]
    public void ConstantsMatchTheTypeScriptPackage()
    {
        Assert.Equal(30 * 60 * 1000, BackgroundAgentConstants.TaskTtlMs);
        Assert.Equal(BackgroundAgentConstants.TaskTtlMs, BackgroundAgentConstants.TerminalTaskTtlMs);
        Assert.Equal(2_700_000, BackgroundAgentConstants.DefaultStaleTimeoutMs);
        Assert.Equal(60_000, BackgroundAgentConstants.DefaultSessionGoneTimeoutMs);
        Assert.Equal(4000, BackgroundAgentConstants.DefaultMaxToolCalls);
        Assert.Equal(20, BackgroundAgentConstants.DefaultCircuitBreakerConsecutiveThreshold);
        Assert.Equal(5000, BackgroundAgentConstants.MinIdleTimeMs);
        Assert.Equal(3000, BackgroundAgentConstants.PollingIntervalMs);
        Assert.Equal(10 * 60 * 1000, BackgroundAgentConstants.TaskCleanupDelayMs);
        Assert.Equal(200, BackgroundAgentConstants.TmuxCallbackDelayMs);
        Assert.Equal(30_000, BackgroundAgentConstants.DefaultWaitForSessionTimeoutMs);
        Assert.Equal(100, BackgroundAgentConstants.DefaultWaitForSessionIntervalMs);
        Assert.Equal(3, BackgroundAgentConstants.DefaultMaxSubagentDepth);
        Assert.Equal(100, BackgroundTaskRegistryConstants.MaxCompletedTaskRegistrySize);
        Assert.Equal(100, BackgroundTaskHistoryConstants.MaxTaskHistoryEntriesPerParent);
        Assert.Equal("session.next.", BackgroundAgentConstants.SessionNextEventPrefix);
        Assert.Contains("running", SessionStatusClassifier.ActiveSessionStatuses);
        Assert.Contains("interrupted", SessionStatusClassifier.KnownTerminalStatuses);
    }

    [Fact]
    public void QueueItemAndTodoRemainPortableContracts()
    {
        var item = new QueueItem
        {
            AttemptId = "att_1",
            Task = CreateTask(),
            Input = new LaunchInput { Description = "d", Prompt = "p", Agent = "a", ParentSessionId = "p", ParentMessageId = "m" },
        };
        var todo = new Todo { Content = "x", Status = "pending", Priority = "high", Id = "t" };
        Assert.Equal("bg_1", item.Task.Id);
        Assert.Equal("high", todo.Priority);
        Assert.Contains(ProcessCleanupEvents.BeforeExit, ProcessCleanupEvents.LifecycleEvents);
        Assert.Contains(BackgroundTaskNotificationStatuses.Error, BackgroundTaskNotificationStatuses.All);
    }

    [Fact]
    public void FormatsDuration()
    {
        var start = DateTime.UnixEpoch;
        Assert.Equal("9s", DurationFormatter.FormatDuration(start, start.AddMilliseconds(9_999)));
        Assert.Equal("1m 5s", DurationFormatter.FormatDuration(start, start.AddMilliseconds(65_000)));
        Assert.Equal("1h 1m 5s", DurationFormatter.FormatDuration(start, start.AddMilliseconds(3_665_000)));
    }

    [Fact]
    public void ErrorClassifiersNormalizeUnknownErrors()
    {
        Assert.True(ErrorClassifier.IsRecord(new Dictionary<string, object?> { ["ok"] = true }));
        Assert.False(ErrorClassifier.IsRecord(null));
        Assert.Equal("InvalidOperationException: bad", ErrorClassifier.GetErrorText(new InvalidOperationException("bad")));
        Assert.Equal("NamedError", ErrorClassifier.GetErrorText(new Dictionary<string, object?> { ["name"] = "NamedError" }));
        Assert.True(ErrorClassifier.IsAbortedSessionError(new Dictionary<string, object?> { ["message"] = "session aborted" }));
        Assert.Equal("ArgumentOutOfRangeException", ErrorClassifier.ExtractErrorName(new ArgumentOutOfRangeException("range")));
        Assert.Equal(
            "nested",
            ErrorClassifier.ExtractErrorMessage(new Dictionary<string, object?>
            {
                ["data"] = new Dictionary<string, object?> { ["error"] = new Dictionary<string, object?> { ["message"] = "nested" } },
            }));
        Assert.Equal("{\"value\":1}", ErrorClassifier.ExtractErrorMessage(new Dictionary<string, object?> { ["value"] = 1 }));
        Assert.Equal(429, ErrorClassifier.ExtractErrorStatusCode(new Dictionary<string, object?> { ["response"] = new Dictionary<string, object?> { ["status"] = "429" } }));
        Assert.Equal("boom", ErrorClassifier.GetSessionErrorMessage(new Dictionary<string, object?> { ["error"] = new Dictionary<string, object?> { ["data"] = new Dictionary<string, object?> { ["message"] = "boom" } } }));
        Assert.Equal("OnlyName", ErrorClassifier.GetErrorText(new Dictionary<string, object?> { ["message"] = 1, ["name"] = "OnlyName" }));
        Assert.Equal("Exception", ErrorClassifier.ExtractErrorName(new Exception("range")));
        var circular = new Dictionary<string, object?>();
        circular["self"] = circular;
        Assert.Contains("Dictionary", ErrorClassifier.ExtractErrorMessage(circular));
        Assert.Null(ErrorClassifier.ExtractErrorStatusCode(new Dictionary<string, object?> { ["status"] = "oops", ["response"] = new Dictionary<string, object?> { ["status"] = "bad" } }));
        Assert.Equal("fallback", ErrorClassifier.GetSessionErrorMessage(new Dictionary<string, object?> { ["error"] = new Dictionary<string, object?> { ["message"] = "fallback" } }));
    }

    [Fact]
    public void SessionStatusClassifierMatchesPackageBehavior()
    {
        var unknown = new List<string>();
        Assert.True(SessionStatusClassifier.IsActiveSessionStatus("busy"));
        Assert.True(SessionStatusClassifier.IsTerminalSessionStatus("interrupted"));
        Assert.False(SessionStatusClassifier.IsTerminalSessionStatus("idle"));
        Assert.False(SessionStatusClassifier.IsActiveSessionStatus("new-status", unknown.Add));
        Assert.Equal(new[] { "new-status" }, unknown);
    }

    [Fact]
    public async Task ConcurrencyManagerUsesConfiguredLimitsAndClearsQueues()
    {
        var manager = new ConcurrencyManager(new BackgroundTaskCoreConfig
        {
            DefaultConcurrency = 1,
            ProviderConcurrency = new Dictionary<string, int> { ["openai"] = 2 },
            ModelConcurrency = new Dictionary<string, int> { ["anthropic/claude"] = 0 },
        });

        Assert.Equal(int.MaxValue, manager.GetConcurrencyLimit("anthropic/claude"));
        Assert.Equal(2, manager.GetConcurrencyLimit("openai/gpt"));
        Assert.Equal(1, manager.GetConcurrencyLimit("x/model"));

        await manager.AcquireAsync("x/model");
        var acquired = false;
        var second = manager.AcquireAsync("x/model").ContinueWith(_ => acquired = true, TaskScheduler.Default);
        Assert.Equal(1, manager.GetCount("x/model"));
        Assert.Equal(1, manager.GetQueueLength("x/model"));
        manager.Release("x/model");
        await second;
        Assert.True(acquired);
        Assert.Equal(1, manager.GetCount("x/model"));
        manager.Release("x/model");
        Assert.Equal(0, manager.GetCount("x/model"));

        await manager.AcquireAsync("m");
        var queued = manager.AcquireAsync("m");
        manager.CancelWaiters("m");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () => await queued);
        Assert.Equal("Concurrency queue cancelled for model: m", failure.Message);
        manager.Clear();
        Assert.Equal(0, manager.GetCount("m"));
        Assert.Equal(0, manager.GetQueueLength("m"));
    }

    [Fact]
    public void LoopDetectorTracksRepeatedToolCalls()
    {
        var settings = LoopDetector.ResolveCircuitBreakerSettings(new BackgroundTaskCoreConfig
        {
            CircuitBreaker = new CircuitBreakerConfig { ConsecutiveThreshold = 2 },
            MaxToolCalls = 3,
        });

        Assert.Equal(new CircuitBreakerSettings { Enabled = true, MaxToolCalls = 3, ConsecutiveThreshold = 2 }, settings);
        Assert.Equal("edit::{\"a\":{\"c\":3,\"d\":4},\"b\":2}", LoopDetector.CreateToolCallSignature("edit", new Dictionary<string, object?>
        {
            ["b"] = 2,
            ["a"] = new Dictionary<string, object?> { ["d"] = 4, ["c"] = 3 },
        }));

        var first = LoopDetector.RecordToolCall(null, "edit", settings, new Dictionary<string, object?> { ["path"] = "a" });
        var second = LoopDetector.RecordToolCall(first, "edit", settings, new Dictionary<string, object?> { ["path"] = "a" });
        Assert.Equal(new ToolLoopDetectionResult { Triggered = false }, LoopDetector.DetectRepetitiveToolUse(first));
        Assert.Equal(new ToolLoopDetectionResult { Triggered = true, ToolName = "edit", RepeatedCount = 2 }, LoopDetector.DetectRepetitiveToolUse(second));
        Assert.Equal("edit::__unknown-input__", LoopDetector.RecordToolCall(second, "edit", settings).LastSignature);
    }

    [Fact]
    public void AttemptLifecycleTracksAttemptsAndRetries()
    {
        var model = new DelegatedModelConfig { ProviderId = "p", ModelId = "m", Variant = "v" };
        var backgroundTask = CreateTask();
        var first = AttemptLifecycle.StartAttempt(backgroundTask, model);
        Assert.Equal(1, first.AttemptNumber);
        Assert.Equal(first.AttemptId, AttemptLifecycle.GetCurrentAttempt(backgroundTask)?.AttemptId);

        var bound = AttemptLifecycle.BindAttemptSession(backgroundTask, first.AttemptId, "session-1", model);
        Assert.Equal(BackgroundTaskStatuses.Running, bound?.Status);
        Assert.Equal(BackgroundTaskStatuses.Running, backgroundTask.Status);
        Assert.Equal(first.AttemptId, AttemptLifecycle.FindAttemptBySession(backgroundTask, "session-1")?.AttemptId);

        var finalized = AttemptLifecycle.FinalizeAttempt(backgroundTask, first.AttemptId, BackgroundTaskStatuses.Error, "failed");
        Assert.Equal("failed", finalized?.Error);
        Assert.Equal(BackgroundTaskStatuses.Error, backgroundTask.Status);

        var retry = AttemptLifecycle.ScheduleRetryAttempt(backgroundTask, first.AttemptId, new DelegatedModelConfig { ProviderId = "p2", ModelId = "m2" }, "failed");
        Assert.Equal(2, retry?.AttemptNumber);
        Assert.Equal(BackgroundTaskStatuses.Pending, backgroundTask.Status);
        Assert.Equal("m2", AttemptLifecycle.ProjectTaskFromCurrentAttempt(backgroundTask).Model?.ModelId);
    }

    [Fact]
    public void EnsureCurrentAttemptPreservesExistingAttempt()
    {
        var backgroundTask = CreateTask();
        backgroundTask.Status = BackgroundTaskStatuses.Running;
        backgroundTask.SessionId = "s";
        var first = AttemptLifecycle.EnsureCurrentAttempt(backgroundTask);
        Assert.Same(first, AttemptLifecycle.EnsureCurrentAttempt(backgroundTask));
    }

    [Fact]
    public void TaskHistoryRecordsUpdatesCapsAndFormatsEntries()
    {
        var history = new TaskHistory();
        history.Record(null, new TaskHistoryEntry { Id = "ignored", Agent = "a", Description = "d", Status = BackgroundTaskStatuses.Pending });
        history.Record("p", new TaskHistoryEntry { Id = "1", Agent = "a", Description = "line\nbreak", Status = BackgroundTaskStatuses.Pending, Category = "quick" });
        history.Record("p", new TaskHistoryEntry { Id = "1", Agent = "a", Description = "done", Status = BackgroundTaskStatuses.Completed, SessionId = "s" });
        Assert.Single(history.GetByParentSession("p"));
        Assert.Contains("session: `s`", history.FormatForCompaction("p"));

        var copy = history.GetByParentSession("p").ToArray();
        copy[0] = new TaskHistoryEntry { Id = "mutated", Agent = "x", Description = "x", Status = BackgroundTaskStatuses.Error };
        Assert.Equal("1", history.GetByParentSession("p")[0].Id);
        history.ClearSession("p");
        Assert.Null(history.FormatForCompaction("p"));

        for (var index = 0; index < 105; index++)
        {
            history.Record("many", new TaskHistoryEntry { Id = index.ToString(), Agent = "a", Description = "d", Status = BackgroundTaskStatuses.Pending });
        }

        Assert.Equal(100, history.GetByParentSession("many").Count);
        Assert.Equal("5", history.GetByParentSession("many")[0].Id);
        history.ClearAll();
        Assert.Empty(history.GetByParentSession("many"));
    }

    [Fact]
    public void TaskRegistryClonesArchivesTrimsAndForgetsTasks()
    {
        TaskRegistry.ClearBackgroundTaskRegistryForTesting();
        var active = CreateTask() with
        {
            SkillContent = "secret",
            SessionPermission = [new SessionPermissionRule { Match = "*", Permission = "allow" }],
        };
        active.Status = BackgroundTaskStatuses.Running;
        active.SessionId = "s";
        active.Progress = new TaskProgress { ToolCalls = 1, LastUpdate = DateTime.UnixEpoch.AddMilliseconds(1), CountedToolPartIds = new HashSet<string>(["part"], StringComparer.Ordinal) };
        TaskRegistry.RememberBackgroundTask(active);
        active.Prompt = "changed";
        var registered = TaskRegistry.GetRegisteredBackgroundTask(active.Id);
        Assert.Equal("[redacted]", registered?.Prompt);
        registered?.Progress?.CountedToolPartIds?.Add("new");
        Assert.DoesNotContain("new", TaskRegistry.GetRegisteredBackgroundTask(active.Id)?.Progress?.CountedToolPartIds ?? []);

        var cloned = TaskRegistry.CloneRegisteredTask(active);
        Assert.Equal("[redacted]", cloned.Prompt);
        Assert.Null(cloned.SkillContent);
        Assert.Null(cloned.SessionPermission);

        TaskRegistry.ArchiveBackgroundTask(active with { Status = BackgroundTaskStatuses.Completed, CompletedAt = DateTime.UnixEpoch.AddMilliseconds(2) });
        Assert.Equal(BackgroundTaskStatuses.Completed, TaskRegistry.GetRegisteredBackgroundTask(active.Id)?.Status);

        for (var index = 0; index < 105; index++)
        {
            TaskRegistry.ArchiveBackgroundTask(CreateTask($"done-{index}") with
            {
                Status = BackgroundTaskStatuses.Completed,
                SessionId = $"s-{index}",
                CompletedAt = DateTime.UnixEpoch.AddMilliseconds(index),
            });
        }

        Assert.Null(TaskRegistry.GetRegisteredBackgroundTask("done-0"));
        TaskRegistry.ForgetBackgroundTask(active.Id);
        Assert.Null(TaskRegistry.GetRegisteredBackgroundTask(active.Id));
        TaskRegistry.ClearBackgroundTaskRegistryForTesting();
    }

    [Fact]
    public void NotificationTemplateBuildsTaskAndAllCompleteReminders()
    {
        var attemptTask = new BackgroundTaskNotificationTask
        {
            Id = "bg_1",
            Description = "Work",
            Status = BackgroundTaskStatuses.Error,
            Error = "boom",
            Attempts =
            [
                new BackgroundTaskAttempt { AttemptId = "a1", AttemptNumber = 1, Status = BackgroundTaskStatuses.Error, ProviderId = "p", ModelId = "m", SessionId = "s1", Error = "bad" },
                new BackgroundTaskAttempt { AttemptId = "a2", AttemptNumber = 2, Status = BackgroundTaskStatuses.Error, ProviderId = "p2", ModelId = "m2", SessionId = "s2" },
            ],
        };

        Assert.Contains("ACTION REQUIRED", BackgroundTaskNotificationTemplate.BuildBackgroundTaskNotificationText(attemptTask, "1s", "ERROR", false, 2, []));
        Assert.Contains("Attempt 1", BackgroundTaskNotificationTemplate.BuildBackgroundTaskNotificationText(attemptTask, "1s", "ERROR", true, 0, [attemptTask]));
        Assert.Contains("ALL BACKGROUND TASKS COMPLETE", BackgroundTaskNotificationTemplate.BuildBackgroundTaskNotificationText(new BackgroundTaskNotificationTask { Id = "ok", Description = string.Empty, Status = BackgroundTaskStatuses.Completed }, "1s", "COMPLETED", true, 0, []));
        Assert.Contains(
            "unknown-model",
            BackgroundTaskNotificationTemplate.BuildBackgroundTaskNotificationText(
                new BackgroundTaskNotificationTask
                {
                    Id = "bg_2",
                    Description = "Model matrix",
                    Status = BackgroundTaskStatuses.Error,
                    Attempts =
                    [
                        new BackgroundTaskAttempt { AttemptId = "x1", AttemptNumber = 1, Status = BackgroundTaskStatuses.Error, ModelId = "m-only" },
                        new BackgroundTaskAttempt { AttemptId = "x2", AttemptNumber = 2, Status = BackgroundTaskStatuses.Error, ProviderId = "p-only" },
                        new BackgroundTaskAttempt { AttemptId = "x3", AttemptNumber = 3, Status = BackgroundTaskStatuses.Error },
                    ],
                },
                "1s",
                "ERROR",
                true,
                0,
                []));
    }

    [Fact]
    public void SessionStreamActivityResolvesNestedAndSessionNextInfo()
    {
        var nested = SessionStreamActivity.ResolveMessagePartInfo(new Dictionary<string, object?>
        {
            ["sessionID"] = "fallback",
            ["part"] = new Dictionary<string, object?>
            {
                ["id"] = "p",
                ["type"] = "tool",
                ["tool"] = "bash",
                ["state"] = new Dictionary<string, object?>
                {
                    ["status"] = "running",
                    ["input"] = new Dictionary<string, object?> { ["command"] = "ls" },
                },
                ["timestamp"] = 100,
            },
        });
        Assert.Equal("fallback", nested?.SessionId);
        Assert.Equal(100, new DateTimeOffset(nested!.ActivityTime!.Value).ToUnixTimeMilliseconds());
        Assert.True(SessionStreamActivity.IsMessagePartForSession(nested, "fallback"));
        Assert.True(SessionStreamActivity.HasOutputSignalFromPart(nested, "fallback"));

        var toolCalled = SessionStreamActivity.ResolveSessionNextPartInfo("session.next.tool.called", new Dictionary<string, object?>
        {
            ["sessionID"] = "s",
            ["callID"] = "c",
            ["tool"] = "read",
            ["input"] = new Dictionary<string, object?> { ["file"] = "x" },
            ["timestamp"] = "1970-01-01T00:00:01.000Z",
        });
        Assert.Equal("tool", toolCalled?.Type);
        Assert.Equal("running", toolCalled?.State?.Status);

        var textDelta = SessionStreamActivity.ResolveSessionNextPartInfo("session.next.text.delta", new Dictionary<string, object?> { ["sessionID"] = "s", ["callID"] = "c" });
        Assert.Equal("text", textDelta?.Field);
        var reasoningDelta = SessionStreamActivity.ResolveSessionNextPartInfo("session.next.reasoning.delta", new Dictionary<string, object?> { ["sessionID"] = "s", ["callID"] = "c" });
        Assert.Equal("reasoning", reasoningDelta?.Field);
        var toolPartial = SessionStreamActivity.ResolveSessionNextPartInfo("session.next.tool.partial", new Dictionary<string, object?> { ["sessionID"] = "s", ["callID"] = "c" });
        Assert.Equal("tool_result", toolPartial?.Type);
        Assert.Null(SessionStreamActivity.ResolveSessionNextPartInfo("other.event", new Dictionary<string, object?>()));
        Assert.False(SessionStreamActivity.HasOutputSignalFromPart(new StreamMessagePartInfo { Type = "text" }));
        Assert.True(SessionStreamActivity.HasOutputSignalFromPart(new StreamMessagePartInfo { SessionId = "s", Field = "reasoning" }, "s"));
    }

    [Fact]
    public async Task SessionActivityAndRefreshTrackLatestProgress()
    {
        Assert.Equal(123, new DateTimeOffset(SessionActivity.ExtractSessionActivityDate(new Dictionary<string, object?> { ["time"] = new Dictionary<string, object?> { ["updated"] = 123L } })!.Value).ToUnixTimeMilliseconds());
        Assert.Null(SessionActivity.ExtractSessionActivityDate(new Dictionary<string, object?> { ["time_updated"] = -1L }));
        Assert.Equal(new SessionActivityLookup.Activity(DateTimeOffset.FromUnixTimeMilliseconds(456).UtcDateTime), SessionActivity.SessionActivityLookupFromInfo(new Dictionary<string, object?> { ["time_updated"] = 456L }));
        Assert.Equal(new SessionActivityLookup.Missing(), SessionActivity.SessionActivityLookupFromInfo(new Dictionary<string, object?>()));

        var backgroundTask = CreateTask();
        backgroundTask.Status = BackgroundTaskStatuses.Running;
        backgroundTask.SessionId = "s";
        backgroundTask.StartedAt = DateTimeOffset.FromUnixTimeMilliseconds(100).UtcDateTime;

        Assert.Equal(new TaskActivityRefreshResult.Activity(200), TaskActivityRefresh.UpdateTaskActivityFromLookup(backgroundTask, new SessionActivityLookup.Activity(DateTimeOffset.FromUnixTimeMilliseconds(200).UtcDateTime)));
        Assert.Equal(200, new DateTimeOffset(backgroundTask.Progress!.LastUpdate).ToUnixTimeMilliseconds());
        Assert.Equal(new TaskActivityRefreshResult.Activity(150), TaskActivityRefresh.UpdateTaskActivityFromLookup(backgroundTask, new SessionActivityLookup.Activity(DateTimeOffset.FromUnixTimeMilliseconds(150).UtcDateTime)));

        var progressedTask = CreateTask();
        progressedTask.Status = BackgroundTaskStatuses.Running;
        progressedTask.SessionId = "s";
        progressedTask.Progress = new TaskProgress { ToolCalls = 0, LastUpdate = DateTimeOffset.FromUnixTimeMilliseconds(100).UtcDateTime };
        Assert.Equal(new TaskActivityRefreshResult.Activity(250), TaskActivityRefresh.UpdateTaskActivityFromLookup(progressedTask, new SessionActivityLookup.Activity(DateTimeOffset.FromUnixTimeMilliseconds(250).UtcDateTime)));
        Assert.Equal(250, new DateTimeOffset(progressedTask.Progress.LastUpdate).ToUnixTimeMilliseconds());

        Assert.Equal(new TaskActivityRefreshResult.Missing(), await TaskActivityRefresh.RefreshTaskActivityFromSession(backgroundTask, _ => Task.FromResult<SessionActivityLookup>(new SessionActivityLookup.Missing())));
        var errors = new List<Exception>();
        Assert.Equal(new TaskActivityRefreshResult.Unavailable(), await TaskActivityRefresh.RefreshTaskActivityFromSession(backgroundTask, _ => throw new InvalidOperationException("nope"), (_, error) => errors.Add(error)));
        Assert.Single(errors);
    }

    [Fact]
    public async Task SubagentSpawnLimitsResolveLineageCyclesAndFailures()
    {
        Assert.Equal(3, SubagentSpawnLimits.GetMaxSubagentDepth());
        Assert.Equal(1, SubagentSpawnLimits.GetMaxSubagentDepth(new BackgroundTaskCoreConfig { MaxDepth = 1 }));

        var reader = new FakeLineageReader(new Dictionary<string, string?>
        {
            ["child"] = "parent",
            ["parent"] = "root",
            ["root"] = null,
        });
        var context = await SubagentSpawnLimits.ResolveSubagentSpawnContextAsync(reader, "child");
        Assert.Equal(new SubagentSpawnContext { RootSessionId = "root", ParentDepth = 2, ChildDepth = 3 }, context);

        var cycle = await Assert.ThrowsAsync<InvalidOperationException>(() => SubagentSpawnLimits.ResolveSubagentSpawnContextAsync(new FakeLineageReader(_ => Task.FromResult<string?>("loop")), "loop"));
        Assert.Contains("cycle", cycle.Message);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => SubagentSpawnLimits.ResolveSubagentSpawnContextAsync(new FakeLineageReader(_ => throw new InvalidOperationException("offline")), "x"));
        Assert.Contains("cannot be enforced safely", failure.Message);
        Assert.Contains("maxDepth=3", SubagentSpawnLimits.CreateSubagentDepthLimitError(4, 3, "p", "r").Message);
    }

    [Fact]
    public void TaskPollerPrunesStaleTasksAndNotifications()
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(120_001).UtcDateTime;
        var stalePending = CreateTask("pending") with { Status = BackgroundTaskStatuses.Pending, QueuedAt = DateTimeOffset.FromUnixTimeMilliseconds(1).UtcDateTime };
        var activeRunning = CreateTask("running") with { Status = BackgroundTaskStatuses.Running, SessionId = "s", StartedAt = DateTimeOffset.FromUnixTimeMilliseconds(1).UtcDateTime, Progress = new TaskProgress { ToolCalls = 0, LastUpdate = DateTimeOffset.FromUnixTimeMilliseconds(1).UtcDateTime } };
        var terminal = CreateTask("done") with { Status = BackgroundTaskStatuses.Completed, SessionId = "done-session", CompletedAt = DateTimeOffset.FromUnixTimeMilliseconds(1).UtcDateTime, StartedAt = DateTimeOffset.FromUnixTimeMilliseconds(1).UtcDateTime };
        var tasks = new Dictionary<string, BackgroundTask> { [stalePending.Id] = stalePending, [activeRunning.Id] = activeRunning, [terminal.Id] = terminal };
        var notifications = new Dictionary<string, List<BackgroundTask>> { ["parent"] = [terminal, CreateTask("old-note") with { StartedAt = DateTime.UnixEpoch }] };
        var pruned = new List<string>();
        var removed = new List<string>();

        TaskPoller.PruneStaleTasksAndNotifications(new PruneStaleTasksArgs
        {
            Tasks = tasks,
            Notifications = notifications,
            TaskTtlMs = 60_000,
            Now = now,
            SessionStatuses = new Dictionary<string, SessionStatusEntry> { ["s"] = new() { Type = "running" } },
            OnTaskPruned = (taskId, backgroundTask, message) => pruned.Add($"{taskId}:{backgroundTask.Status}:{message}"),
            OnTerminalTaskRemoved = (taskId, _) => removed.Add(taskId),
        });

        Assert.Contains("pending:pending:Task timed out while queued", pruned[0]);
        Assert.Empty(removed);
        Assert.True(tasks.ContainsKey("done"));
        Assert.False(notifications.ContainsKey("parent"));

        var terminalOnly = CreateTask("done") with { Status = BackgroundTaskStatuses.Completed, SessionId = "s", CompletedAt = DateTimeOffset.FromUnixTimeMilliseconds(1).UtcDateTime };
        var terminalTasks = new Dictionary<string, BackgroundTask> { [terminalOnly.Id] = terminalOnly };
        var terminalRemoved = new List<string>();
        TaskPoller.PruneStaleTasksAndNotifications(new PruneStaleTasksArgs
        {
            Tasks = terminalTasks,
            Notifications = new Dictionary<string, List<BackgroundTask>>(),
            Now = DateTimeOffset.FromUnixTimeMilliseconds(BackgroundAgentConstants.TerminalTaskTtlMs + 2).UtcDateTime,
            OnTaskPruned = (_, _, _) => { },
            OnTerminalTaskRemoved = (id, _) => terminalRemoved.Add(id),
        });
        Assert.False(terminalTasks.ContainsKey("done"));
        Assert.Equal(new[] { "done" }, terminalRemoved);

        var emptyNotifications = new Dictionary<string, List<BackgroundTask>> { ["empty"] = [] };
        TaskPoller.PruneStaleTasksAndNotifications(new PruneStaleTasksArgs
        {
            Tasks = new Dictionary<string, BackgroundTask>(),
            Notifications = emptyNotifications,
            Now = DateTimeOffset.FromUnixTimeMilliseconds(1000).UtcDateTime,
            TaskTtlMs = 500,
            OnTaskPruned = (_, _, _) => { },
        });
        Assert.False(emptyNotifications.ContainsKey("empty"));

        var partialNotifications = new Dictionary<string, List<BackgroundTask>>
        {
            ["parent"] =
            [
                CreateTask("fresh") with { StartedAt = DateTimeOffset.FromUnixTimeMilliseconds(900).UtcDateTime },
                CreateTask("old") with { StartedAt = DateTime.UnixEpoch },
            ],
        };
        TaskPoller.PruneStaleTasksAndNotifications(new PruneStaleTasksArgs
        {
            Tasks = new Dictionary<string, BackgroundTask>(),
            Notifications = partialNotifications,
            Now = DateTimeOffset.FromUnixTimeMilliseconds(1000).UtcDateTime,
            TaskTtlMs = 150,
            OnTaskPruned = (_, _, _) => { },
        });
        Assert.Equal(new[] { "fresh" }, partialNotifications["parent"].Select(item => item.Id));
    }

    [Fact]
    public async Task WaitForTaskSessionHandlesImmediateDelayedAndTerminalStates()
    {
        Assert.Equal("s", await WaitForTaskSession.WaitForTaskSessionIdAsync(new FakeTaskSessionReader(_ => new BackgroundTaskSessionSnapshot { SessionId = "s" }), "id", new WaitForTaskSessionIdOptions { TimeoutMs = 1, IntervalMs = 1 }));
        Assert.Null(await WaitForTaskSession.WaitForTaskSessionIdAsync(new FakeTaskSessionReader(_ => new BackgroundTaskSessionSnapshot { Status = BackgroundTaskStatuses.Error }), "id", new WaitForTaskSessionIdOptions { TimeoutMs = 1, IntervalMs = 1 }));
        Assert.Null(await WaitForTaskSession.WaitForTaskSessionIdAsync(new FakeTaskSessionReader(_ => new BackgroundTaskSessionSnapshot { Status = BackgroundTaskStatuses.Pending }), "id", new WaitForTaskSessionIdOptions { TimeoutMs = 2, IntervalMs = 1 }));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Null(await WaitForTaskSession.WaitForTaskSessionIdAsync(new FakeTaskSessionReader(_ => new BackgroundTaskSessionSnapshot { Status = BackgroundTaskStatuses.Pending }), "id", new WaitForTaskSessionIdOptions { CancellationToken = cts.Token }));

        var calls = 0;
        var sessionId = await WaitForTaskSession.WaitForTaskSessionIdAsync(new FakeTaskSessionReader(_ =>
        {
            calls++;
            return calls > 1 ? new BackgroundTaskSessionSnapshot { SessionId = "later" } : new BackgroundTaskSessionSnapshot { Status = BackgroundTaskStatuses.Pending };
        }), "id", new WaitForTaskSessionIdOptions { TimeoutMs = 20, IntervalMs = 1 });
        Assert.Equal("later", sessionId);
    }

    private static BackgroundTask CreateTask(string id = "bg_1")
    {
        return new BackgroundTask
        {
            Id = id,
            ParentSessionId = "parent",
            ParentMessageId = "msg",
            Description = "Do work",
            Prompt = "work",
            Agent = "general",
            Status = BackgroundTaskStatuses.Pending,
        };
    }

    private sealed class FakeLineageReader : ISessionLineageReader
    {
        private readonly Func<string, Task<string?>> getParentSessionIdAsync;

        public FakeLineageReader(IReadOnlyDictionary<string, string?> values)
        {
            getParentSessionIdAsync = sessionId => Task.FromResult(values.TryGetValue(sessionId, out var parent) ? parent : null);
        }

        public FakeLineageReader(Func<string, Task<string?>> getParentSessionIdAsync)
        {
            this.getParentSessionIdAsync = getParentSessionIdAsync;
        }

        public Task<string?> GetParentSessionIdAsync(string sessionId)
        {
            return getParentSessionIdAsync(sessionId);
        }
    }

    private sealed class FakeTaskSessionReader : ITaskSessionReader
    {
        private readonly Func<string, BackgroundTaskSessionSnapshot?> getTask;

        public FakeTaskSessionReader(Func<string, BackgroundTaskSessionSnapshot?> getTask)
        {
            this.getTask = getTask;
        }

        public BackgroundTaskSessionSnapshot? GetTask(string taskId)
        {
            return getTask(taskId);
        }
    }
}
