using Lfe.Hooks;
using Lfe.SessionManager;
using Lfe.SkillMcp;
using Lfe.SkillsCore;
using Lfe.UlwHostContract;
using Lfe.UlwKernel;
using Lfe.UlwLoopState;

namespace Lfe.StandaloneRuntime;

public sealed class StandaloneLfeRuntime
{
    public IUlwHost Host { get; }
    public UlwLoopStateController LoopState { get; }
    public IUlwLoopEngine Engine { get; }
    public List<BuiltinSkill> Skills { get; }
    public List<LfeHookDefinition> Hooks { get; }
    public List<PortableToolDefinition> Tools { get; }
    public List<string> SkillMcpServerNames { get; }
    public List<UlwPromptRequest> DispatchedPrompts { get; }

    private readonly Dictionary<string, List<UlwMessage>> _messagesBySession;
    private readonly Dictionary<string, List<TodoItem>> _todosBySession;
    private readonly HashSet<Action<UlwSessionEvent>> _listeners;

    private StandaloneLfeRuntime(
        IUlwHost host,
        UlwLoopStateController loopState,
        IUlwLoopEngine engine,
        List<BuiltinSkill> skills,
        List<LfeHookDefinition> hooks,
        List<PortableToolDefinition> tools,
        List<string> skillMcpServerNames,
        List<UlwPromptRequest> dispatchedPrompts,
        Dictionary<string, List<UlwMessage>> messagesBySession,
        Dictionary<string, List<TodoItem>> todosBySession,
        HashSet<Action<UlwSessionEvent>> listeners)
    {
        Host = host;
        LoopState = loopState;
        Engine = engine;
        Skills = skills;
        Hooks = hooks;
        Tools = tools;
        SkillMcpServerNames = skillMcpServerNames;
        DispatchedPrompts = dispatchedPrompts;
        _messagesBySession = messagesBySession;
        _todosBySession = todosBySession;
        _listeners = listeners;
    }

    public static StandaloneLfeRuntime Create(PortableToolRuntimeOptions? toolRuntime = null)
    {
        var dispatchedPrompts = new List<UlwPromptRequest>();
        var messagesBySession = new Dictionary<string, List<UlwMessage>>();
        var todosBySession = new Dictionary<string, List<TodoItem>>();
        var listeners = new HashSet<Action<UlwSessionEvent>>();

        IUlwHost host = new InMemoryUlwHost(messagesBySession, dispatchedPrompts, listeners);

        var loopState = new UlwLoopStateController(new MemoryUlwLoopStateStore());
        var skills = SkillCatalog.CreateBuiltinSkills(new CreateBuiltinSkillsOptions(TeamModeEnabled: true));
        var hooks = HookDefinitions.ListLfeHooks();
        var baseTools = PortableTools.CreatePortableTools(toolRuntime);
        var skillMcpSkillLikes = skills.Select(s => new SkillMcpSkillLike(
            s.Name,
            s.McpConfig is not null
                ? new Lfe.SkillMcp.SkillMcpConfig(
                    s.McpConfig.Servers.ToDictionary(
                        kv => kv.Key,
                        kv => new Lfe.SkillMcp.SkillMcpServerConfig(kv.Value.Command, kv.Value.Args, kv.Value.Env)))
                : null)).ToList();
        var skillMcpTool = PortableSkillMcp.CreatePortableSkillMcpTool(skillMcpSkillLikes);
        var tools = baseTools.Append(skillMcpTool).ToList();
        var skillMcpServerNames = PortableSkillMcp.ListPortableSkillMcpServers(skillMcpSkillLikes);

        var engine = UlwKernelRuntime.CreateUlwLoopEngine(new UlwLoopEngineOptions(host, loopState));

        return new StandaloneLfeRuntime(host, loopState, engine, skills, hooks, tools, skillMcpServerNames, dispatchedPrompts,
            messagesBySession, todosBySession, listeners);
    }

    public async Task SubmitUserMessageAsync(string sessionID, string text)
    {
        AppendMessage(sessionID, new UlwMessage("user", text));
        await UlwKernelRuntime.RunTrackedUlwAsync(new RunTrackedUlwInput(
            Host, sessionID, text, LoopState));
    }

    public void AppendAssistantMessage(string sessionID, string text)
        => AppendMessage(sessionID, new UlwMessage("assistant", text));

    public void SetTodos(string sessionID, List<TodoItem> todos)
        => _todosBySession[sessionID] = [.. todos];

    public async Task EmitIdleAsync(string sessionID)
    {
        var evt = new UlwSessionEvent(UlwSessionEventType.Idle, sessionID);
        foreach (var listener in _listeners)
            listener(evt);
        await Task.Yield();
    }

    public IReadOnlyList<UlwMessage> ReadMessages(string sessionID)
        => _messagesBySession.TryGetValue(sessionID, out var msgs) ? msgs : [];

    public string ListSessions(SessionListArgs? args = null)
    {
        var records = BuildSessionRecords();
        return SessionFormatter.FormatSessionList(SessionFormatter.SelectSessionRecords(records, args));
    }

    public string ReadSession(SessionReadArgs args)
    {
        var records = BuildSessionRecords();
        var record = records.FirstOrDefault(r => r.Info.Id == args.SessionId);
        return record is not null ? SessionFormatter.ReadSessionRecord(record, args) : "No messages found in this session.";
    }

    public string SearchSessions(SessionSearchArgs args)
    {
        var records = BuildSessionRecords();
        return SessionFormatter.FormatSearchResults(SessionFormatter.SearchSessionRecords(records, args));
    }

    public string GetSessionInfo(string sessionID)
    {
        var records = BuildSessionRecords();
        var record = records.FirstOrDefault(r => r.Info.Id == sessionID);
        return record is not null ? SessionFormatter.FormatSessionInfo(record.Info) : "No valid sessions found.";
    }

    public async Task<string> ExecuteToolAsync(string name, Dictionary<string, object?> parameters)
        => await PortableTools.ExecutePortableToolAsync(Tools, name, parameters);

    public void Stop() => Engine.Stop();

    private void AppendMessage(string sessionID, UlwMessage message)
    {
        if (!_messagesBySession.TryGetValue(sessionID, out var list))
        {
            list = [];
            _messagesBySession[sessionID] = list;
        }
        list.Add(message);
    }

    private List<SessionRecord> BuildSessionRecords()
    {
        var records = new List<SessionRecord>();
        foreach (var (sessionID, messages) in _messagesBySession)
        {
            var sessionMessages = messages.Select((m, i) => new SessionMessage(
                Id: $"{sessionID}-msg-{i + 1}",
                Role: m.Role,
                Text: m.Text,
                CreatedAt: i + 1)).ToList();

            var todos = _todosBySession.TryGetValue(sessionID, out var t) ? t.ToArray() : null;
            var info = SessionFormatter.BuildSessionInfo(sessionID, sessionMessages, todos, sessionMessages.Count);
            if (info is null) continue;
            records.Add(new SessionRecord(info, sessionMessages, todos, sessionMessages.Count));
        }

        return records
            .OrderByDescending(r => r.Info.LastMessage?.Ticks ?? 0)
            .ToList();
    }
}

/// <summary>
/// In-memory IUlwHost implementation for standalone runtime.
/// </summary>
file sealed class InMemoryUlwHost : IUlwHost
{
    private readonly Dictionary<string, List<UlwMessage>> _messagesBySession;
    private readonly List<UlwPromptRequest> _dispatchedPrompts;
    private readonly HashSet<Action<UlwSessionEvent>> _listeners;

    public InMemoryUlwHost(
        Dictionary<string, List<UlwMessage>> messagesBySession,
        List<UlwPromptRequest> dispatchedPrompts,
        HashSet<Action<UlwSessionEvent>> listeners)
    {
        _messagesBySession = messagesBySession;
        _dispatchedPrompts = dispatchedPrompts;
        _listeners = listeners;
    }

    public Task<UlwPromptReceipt> DispatchPromptAsync(UlwPromptRequest request)
    {
        _dispatchedPrompts.Add(request);
        AppendMessage(request.SessionId, new UlwMessage("user", request.Message));
        return Task.FromResult(new UlwPromptReceipt(
            Accepted: true,
            SessionId: request.SessionId,
            DispatchId: $"runtime-dispatch-{_dispatchedPrompts.Count}"));
    }

    public Task<IReadOnlyList<UlwMessage>> ReadMessagesAsync(string sessionId)
    {
        _messagesBySession.TryGetValue(sessionId, out var msgs);
        return Task.FromResult<IReadOnlyList<UlwMessage>>(msgs ?? []);
    }

    public Task<IReadOnlyList<UlwTodo>> ReadTodosAsync(string sessionId)
        => Task.FromResult<IReadOnlyList<UlwTodo>>([]);

    public Task<string> ReadStatusAsync(string sessionId)
        => Task.FromResult("idle");

    public Task AbortAsync(string sessionId) => Task.CompletedTask;

    public Action OnEvent(Action<UlwSessionEvent> listener)
    {
        _listeners.Add(listener);
        return () => _listeners.Remove(listener);
    }

    private void AppendMessage(string sessionID, UlwMessage message)
    {
        if (!_messagesBySession.TryGetValue(sessionID, out var list))
        {
            list = [];
            _messagesBySession[sessionID] = list;
        }
        list.Add(message);
    }
}
