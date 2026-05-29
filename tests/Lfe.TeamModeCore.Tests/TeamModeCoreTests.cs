namespace Lfe.TeamModeCore.Tests;

public sealed class TeamModeCoreTests
{
    private static TeamSpec ValidSpec() => new()
    {
        Name = "demo-team",
        LeadAgentId = "lead",
        Members =
        [
            new SubagentMember { Name = "lead", SubagentType = "sisyphus" },
            new CategoryMember { Name = "worker", Category = "quick", Prompt = "Perform a focused implementation task." },
        ],
    };

    [Fact]
    public void ExportsPublicSurface()
    {
        Assert.NotNull(MemberParser.ParseMember(new { name = "member", category = "quick", prompt = "Do enough work." }));
        Assert.NotNull(TaskList.CreateEmptyTaskListState());
        Assert.NotNull(Mailbox.CreateEmptyMailboxState());
        Assert.NotNull(TeamSessionRegistry.CreateTeamSessionRegistryState());
    }

    [Fact]
    public void ParsesLegacyMemberShapeAndHardRejectsReadOnlyAgents()
    {
        var member = MemberParser.ParseMember(new { name = "worker", category = "quick", prompt = "Do enough work." });
        Assert.Equal("worker", member.Name);
        Assert.Equal("category", member.Kind);
        Assert.Equal("quick", member.Category);

        var ex = Assert.Throws<MemberValidationError>(() => MemberParser.ParseMember(new { name = "reader", subagent_type = "oracle" }));
        Assert.Contains("read-only", ex.Message);
    }

    [Fact]
    public void ParsesTeamSpecAndNormalizesMembers()
    {
        var parsed = TeamSpecRegistry.ParseTeamSpec(new
        {
            name = "Project Analysis Team",
            members = new object[]
            {
                new { role = "Researcher", description = "Inspect the repository", capabilities = new[] { "read", "summarize" } },
                new { kind = "worker", responsibilities = new[] { "implement", "verify" } },
            },
        }, new NormalizeTeamSpecInputOptions
        {
            CallerTeamLead = new CallerTeamLead { AgentTypeId = "sisyphus", DisplayName = "Sisyphus", IsEligibleForTeamLead = true },
            DefaultCategoryName = "quick",
        });

        Assert.Equal("project-analysis-team", parsed.Name);
        Assert.Equal("lead", parsed.LeadAgentId);
        Assert.Equal(["lead", "quick-1", "quick-2"], parsed.Members.Select(member => member.Name).ToArray());
        var category = Assert.IsType<CategoryMember>(parsed.Members[1]);
        Assert.Equal("Role: Researcher\nInspect the repository\nread, summarize", category.Prompt);
    }

    [Fact]
    public void ValidatesSpecAndFormatsIssues()
    {
        var safe = TeamSpecSchema.SafeParse(new { name = "bad name!", members = Array.Empty<object>() });
        Assert.False(safe.Success);
        Assert.Contains("name", TeamSpecRegistry.FormatTeamSpecIssues(safe.Error!));

        var valid = TeamSpecSchema.Parse(ValidSpec());
        Assert.Equal("lead", valid.LeadAgentId);

        var ex = Assert.Throws<TeamSpecValidationError>(() => TeamSpecRegistry.ValidateSpec(valid with { Members = [valid.Members[0], valid.Members[0]] }));
        Assert.Equal("DUPLICATE_MEMBER_NAME", ex.Code);
    }

    [Fact]
    public void ResolvesPathsAndMergesSpecs()
    {
        Assert.Equal("/tmp/omo", TeamModeCorePaths.ResolveBaseDir(new TeamModeCorePathConfig { BaseDir = "/tmp/omo" }));
        Assert.Equal("/tmp/omo/runtime/run-1/tasks", TeamModeCorePaths.GetTasksDir("/tmp/omo", "run-1"));

        var merged = TeamModeCorePaths.MergeDiscoveredTeamSpecs(
            [new TeamSpecEntry { Name = "alpha", Scope = "project", Path = "/project/alpha" }],
            [new TeamSpecEntry { Name = "alpha", Scope = "user", Path = "/user/alpha" }, new TeamSpecEntry { Name = "beta", Scope = "user", Path = "/user/beta" }]);

        Assert.Equal(["alpha", "beta"], merged.Select(entry => entry.Name).ToArray());
    }

    [Fact]
    public void HandlesRuntimeMailboxAndSessionState()
    {
        var runtime = RuntimeStateManager.CreateRuntimeStateFromSpec(ValidSpec(), new CreateRuntimeStateOptions
        {
            TeamRunId = "11111111-1111-4111-8111-111111111111",
            SpecSource = "project",
            CreatedAt = 100,
            LeadSessionId = "lead-session",
            Bounds = new RuntimeBounds { MaxParallelMembers = 2 },
        });

        Assert.Equal("demo-team", runtime.TeamName);
        Assert.Equal("leader", runtime.Members[0].AgentType);

        var mailbox = Mailbox.CreateEmptyMailboxState(["lead", "worker"]);
        var sent = Mailbox.SendMessageToState(mailbox, new Message { MessageId = "m1", From = "lead", To = "worker", Body = "Hello <worker> & welcome", Timestamp = 200 }, new SendContext(["worker"], true), new SendMessageLimits { MessagePayloadMaxBytes = 32 * 1024, RecipientUnreadMaxBytes = 64 * 1024 }, runtime);
        Assert.Single(sent.DeliveredTo);

        var injected = Mailbox.PollAndBuildInjectionFromState(runtime, sent.State, "worker", "turn-1");
        Assert.True(injected.Injected);
        Assert.Equal(["m1"], injected.MessageIds);

        var registry = TeamSessionRegistry.CreateTeamSessionRegistryState();
        registry = TeamSessionRegistry.RegisterTeamSessionInState(registry, "s1", new TeamSessionEntry { TeamRunId = runtime.TeamRunId, MemberName = "lead", Role = "lead" });
        Assert.Equal("lead", TeamSessionRegistry.LookupTeamSessionInState(registry, "s1")?.MemberName);
    }
}
