using System.Text.Json;
using Lfe.Hooks;
using Lfe.SessionManager;
using Lfe.SkillMcp;
using Lfe.SkillsCore;
using Lfe.SlashCommand;
using Lfe.UlwHostContract;
using Lfe.UlwKernel;
using Lfe.UlwLoopState;
using Lfe.CodexAdapter;

namespace Lfe.StandaloneRuntime;

#region Codex DTOs

public sealed record CodexConversationInput(string ConversationID);

public sealed record CodexSendInput(
    string ConversationID,
    string Input,
    string? Agent = null,
    string? Model = null);

public sealed record CodexSendResult(
    string? Id = null,
    string? ItemID = null,
    bool? Accepted = null,
    string? Status = null,
    string? Error = null);

public sealed record CodexTranscriptItem(
    string? Role = null,
    string? Type = null,
    string? Text = null,
    string? ContentText = null,
    List<CodexContentPart>? ContentParts = null);

public sealed record CodexContentPart(string? Type = null, string? Text = null);

public sealed record CodexConversationEvent(
    string Type,
    string ConversationID);

public sealed record CodexStatusResult(string? Status = null);

public interface ICodexConversationClient
{
    Task<CodexSendResult> SendAsync(CodexSendInput input);
    Task<List<CodexTranscriptItem>> TranscriptAsync(CodexConversationInput input);
    Task<CodexStatusResult?> GetStatusAsync(CodexConversationInput input);
    Task AbortAsync(CodexConversationInput input);
    Action? SubscribeEvents(Action<CodexConversationEvent> listener);
}

#endregion

public sealed class CodexComposedOmoRuntime
{
    public IUlwHost Host { get; }
    public UlwLoopStateController LoopState { get; }
    public IUlwLoopEngine Engine { get; }
    public List<BuiltinSkill> Skills { get; }
    public List<OmoHookDefinition> Hooks { get; }
    public List<HookSlashCommandInfo> SlashCommands { get; }
    public List<PortableToolDefinition> Tools { get; }
    public List<string> SkillMcpServerNames { get; }
    public List<UlwPromptRequest> DispatchedPrompts { get; }

    private readonly ICodexConversationClient? _client;

    private CodexComposedOmoRuntime(
        IUlwHost host,
        UlwLoopStateController loopState,
        IUlwLoopEngine engine,
        List<BuiltinSkill> skills,
        List<OmoHookDefinition> hooks,
        List<HookSlashCommandInfo> slashCommands,
        List<PortableToolDefinition> tools,
        List<string> skillMcpServerNames,
        List<UlwPromptRequest> dispatchedPrompts,
        ICodexConversationClient? client)
    {
        Host = host;
        LoopState = loopState;
        Engine = engine;
        Skills = skills;
        Hooks = hooks;
        SlashCommands = slashCommands;
        Tools = tools;
        SkillMcpServerNames = skillMcpServerNames;
        DispatchedPrompts = dispatchedPrompts;
        _client = client;
    }

    /// <summary>
    /// Canonical composition path — uses the CodexAdapter spawn+JSONL transport
    /// to drive a real Codex CLI process. This is the recommended entrypoint.
    /// </summary>
    public static CodexComposedOmoRuntime CreateFromAdapter(CodexAdapterOptions options)
    {
        var runtime = CodexAdapterFactory.Create(options);
        return Compose(runtime.Host, client: null, runtime);
    }

    /// <summary>
    /// Legacy composition path — requires an external ICodexConversationClient implementation.
    /// Retained for backward compatibility; prefer <see cref="CreateFromAdapter"/> for new code.
    /// </summary>
    public static CodexComposedOmoRuntime Create(ICodexConversationClient client)
    {
        var dispatchedPrompts = new List<UlwPromptRequest>();
        IUlwHost host = new CodexUlwHost(client, dispatchedPrompts);
        return Compose(host, client, adapterRuntime: null);
    }

    private static CodexComposedOmoRuntime Compose(
        IUlwHost host,
        ICodexConversationClient? client,
        global::Lfe.CodexAdapter.CodexAdapterRuntime? adapterRuntime)
    {
        var loopState = new UlwLoopStateController(new MemoryUlwLoopStateStore());
        var engine = UlwKernelRuntime.CreateUlwLoopEngine(new UlwLoopEngineOptions(host, loopState));
        var skills = SkillCatalog.CreateBuiltinSkills(new CreateBuiltinSkillsOptions(TeamModeEnabled: true));
        var hooks = HookDefinitions.ListOmoHooks();
        var slashCommands = Discovery.ToHookSlashCommandInfos(Discovery.DiscoverSlashCommandsSync());
        var baseTools = PortableTools.CreatePortableTools();
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

        return new CodexComposedOmoRuntime(
            host, loopState, engine, skills, hooks, slashCommands,
            tools, skillMcpServerNames, [], client);
    }

    public async Task SubmitUserMessageAsync(string sessionID, string text, string? agentName = null, string? modelID = null)
    {
        await UlwKernelRuntime.RunTrackedUlwAsync(new RunTrackedUlwInput(
            Host, sessionID, text, LoopState,
            AgentName: agentName, ModelId: modelID));
    }

    public async Task<IReadOnlyList<UlwMessage>> ReadMessagesAsync(string sessionID)
        => await Host.ReadMessagesAsync(sessionID);

    public async Task<string?> ExpandSlashCommandAsync(string text)
    {
        var output = text;
        foreach (var cmd in SlashCommands)
        {
            if (text.StartsWith($"/{cmd.Name}", StringComparison.OrdinalIgnoreCase))
            {
                output = $"/{cmd.Name} {cmd.Description}";
                break;
            }
        }
        return output != text ? output : null;
    }

    public async Task<string> ListSessionsAsync(SessionListArgs? args = null)
    {
        var records = await BuildCodexSessionRecordsAsync(Host, ["ses_codex"]);
        return SessionFormatter.FormatSessionList(SessionFormatter.SelectSessionRecords(records, args));
    }

    public async Task<string> ReadSessionAsync(SessionReadArgs args)
    {
        var records = await BuildCodexSessionRecordsAsync(Host, [args.SessionId]);
        var record = records.FirstOrDefault(r => r.Info.Id == args.SessionId);
        return record is not null ? SessionFormatter.ReadSessionRecord(record, args) : "No messages found in this session.";
    }

    public async Task<string> SearchSessionsAsync(SessionSearchArgs args)
    {
        var sessionIDs = args.SessionId is not null ? new List<string> { args.SessionId } : new List<string> { "ses_codex" };
        var records = await BuildCodexSessionRecordsAsync(Host, sessionIDs);
        return SessionFormatter.FormatSearchResults(SessionFormatter.SearchSessionRecords(records, args));
    }

    public async Task<string> GetSessionInfoAsync(string sessionID)
    {
        var records = await BuildCodexSessionRecordsAsync(Host, [sessionID]);
        var record = records.FirstOrDefault(r => r.Info.Id == sessionID);
        return record is not null ? SessionFormatter.FormatSessionInfo(record.Info) : "No valid sessions found.";
    }

    public async Task<string> ExecuteToolAsync(string name, Dictionary<string, object?> parameters)
        => await PortableTools.ExecutePortableToolAsync(Tools, name, parameters);

    public void Stop() => Engine.Stop();

    private static async Task<List<SessionRecord>> BuildCodexSessionRecordsAsync(IUlwHost host, IReadOnlyList<string> sessionIDs)
    {
        var records = new List<SessionRecord>();
        foreach (var sessionID in sessionIDs)
        {
            var messages = await host.ReadMessagesAsync(sessionID);
            var todos = await host.ReadTodosAsync(sessionID);
            var sessionMessages = messages.Select((m, i) => new SessionMessage(
                Id: $"{sessionID}-msg-{i + 1}",
                Role: m.Role,
                Text: m.Text,
                CreatedAt: i + 1)).ToList();

            var todosConverted = todos.Select(t => new TodoItem(null, t.Content, t.Status)).ToArray();
            var info = SessionFormatter.BuildSessionInfo(sessionID, sessionMessages, todosConverted, sessionMessages.Count);
            if (info is null) continue;
            records.Add(new SessionRecord(info, sessionMessages, todosConverted, sessionMessages.Count));
        }
        return records;
    }
}

/// <summary>
/// Codex-specific IUlwHost adapter that translates between Codex client and omo protocol.
/// </summary>
file sealed class CodexUlwHost : IUlwHost
{
    private readonly ICodexConversationClient _client;
    private readonly List<UlwPromptRequest> _dispatchedPrompts;

    public CodexUlwHost(ICodexConversationClient client, List<UlwPromptRequest> dispatchedPrompts)
    {
        _client = client;
        _dispatchedPrompts = dispatchedPrompts;
    }

    public async Task<UlwPromptReceipt> DispatchPromptAsync(UlwPromptRequest request)
    {
        _dispatchedPrompts.Add(request);
        var sendInput = new CodexSendInput(
            request.SessionId,
            request.Message,
            request.AgentName,
            request.ModelId);
        var result = await _client.SendAsync(sendInput);
        return NormalizeSendResult(request, result);
    }

    public async Task<IReadOnlyList<UlwMessage>> ReadMessagesAsync(string sessionId)
    {
        var result = await _client.TranscriptAsync(new CodexConversationInput(sessionId));
        return NormalizeMessages(result);
    }

    public Task<IReadOnlyList<UlwTodo>> ReadTodosAsync(string sessionId)
        => Task.FromResult<IReadOnlyList<UlwTodo>>([]);

    public async Task<string> ReadStatusAsync(string sessionId)
    {
        var result = await _client.GetStatusAsync(new CodexConversationInput(sessionId));
        return result?.Status ?? "unknown";
    }

    public async Task AbortAsync(string sessionId)
        => await _client.AbortAsync(new CodexConversationInput(sessionId));

    public Action OnEvent(Action<UlwSessionEvent> listener)
    {
        var unsubscribe = _client.SubscribeEvents(evt =>
        {
            if (evt.Type == "idle")
                listener(new UlwSessionEvent(UlwSessionEventType.Idle, evt.ConversationID));
        });
        return unsubscribe ?? (() => { });
    }

    private static UlwPromptReceipt NormalizeSendResult(UlwPromptRequest request, CodexSendResult result)
    {
        var accepted = result.Error is null
            && result.Status != "failed"
            && result.Status != "rejected"
            && result.Accepted != false;
        return new UlwPromptReceipt(
            Accepted: accepted,
            SessionId: request.SessionId,
            DispatchId: result.Id ?? result.ItemID ?? request.SessionId);
    }

    private static List<UlwMessage> NormalizeMessages(List<CodexTranscriptItem> items)
        => items
            .Select(item => new
            {
                Role = NormalizeRole(item.Role),
                Text = CollectText(item),
            })
            .Where(x => x.Role is not null && x.Text.Length > 0)
            .Select(x => new UlwMessage(x.Role!, x.Text))
            .ToList();

    private static string? NormalizeRole(string? role)
        => role is "user" or "assistant" or "system" or "tool" ? role : null;

    private static string CollectText(CodexTranscriptItem item)
    {
        if (item.Text is not null) return item.Text;
        if (item.ContentText is not null) return item.ContentText;
        if (item.ContentParts is not null)
            return string.Join("\n", item.ContentParts.Where(p => p.Text is not null).Select(p => p.Text));
        return "";
    }
}
