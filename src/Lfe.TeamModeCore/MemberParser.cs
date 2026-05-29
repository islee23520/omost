using System.Text.Json;

namespace Lfe.TeamModeCore;

public enum AgentEligibilityVerdict
{
    Eligible,
    Conditional,
    HardReject,
}

public sealed record AgentEligibilityEntry
{
    public AgentEligibilityVerdict Verdict { get; init; }

    public string? RejectionMessage { get; init; }
}

public sealed class MemberValidationError : Exception
{
    public MemberValidationError(string message, string? memberName = null, string? issue = null)
        : base(message)
    {
        MemberName = memberName;
        Issue = issue;
    }

    public string? MemberName { get; }

    public string? Issue { get; }
}

public static class MemberParser
{
    public static readonly IReadOnlyDictionary<string, AgentEligibilityEntry> AgentEligibilityRegistry = new Dictionary<string, AgentEligibilityEntry>(StringComparer.Ordinal)
    {
        ["sisyphus"] = new() { Verdict = AgentEligibilityVerdict.Eligible },
        ["hephaestus"] = new()
        {
            Verdict = AgentEligibilityVerdict.Conditional,
            RejectionMessage = "Agent 'hephaestus' lacks teammate permission. Either apply D-36 (add teammate: \"allow\" in tool-config-handler.ts) or use subagent_type: \"sisyphus\" instead.",
        },
        ["oracle"] = new()
        {
            Verdict = AgentEligibilityVerdict.HardReject,
            RejectionMessage = "Agent 'oracle' is read-only (cannot write files). Team members must write to mailbox inbox files. Use delegate-task with subagent_type: 'oracle' for read-only analysis instead.",
        },
        ["librarian"] = new()
        {
            Verdict = AgentEligibilityVerdict.HardReject,
            RejectionMessage = "Agent 'librarian' is read-only (write/edit denied). Cannot write to mailbox as team member. Use delegate-task for research queries instead.",
        },
        ["explore"] = new()
        {
            Verdict = AgentEligibilityVerdict.HardReject,
            RejectionMessage = "Agent 'explore' is read-only (write/edit denied). Cannot write to mailbox as team member. Use delegate-task for codebase exploration instead.",
        },
        ["multimodal-looker"] = new()
        {
            Verdict = AgentEligibilityVerdict.HardReject,
            RejectionMessage = "Agent 'multimodal-looker' has read-only tool access (only 'read' allowed). Cannot write to mailbox as team member.",
        },
        ["metis"] = new()
        {
            Verdict = AgentEligibilityVerdict.HardReject,
            RejectionMessage = "Agent 'metis' is read-only (pre-planning consultant). Cannot write to mailbox as team member. Use delegate-task for pre-planning analysis instead.",
        },
        ["momus"] = new()
        {
            Verdict = AgentEligibilityVerdict.HardReject,
            RejectionMessage = "Agent 'momus' is read-only (plan reviewer). Cannot write to mailbox as team member. Use delegate-task for plan review instead.",
        },
        ["atlas"] = new() { Verdict = AgentEligibilityVerdict.Eligible },
        ["prometheus"] = new()
        {
            Verdict = AgentEligibilityVerdict.HardReject,
            RejectionMessage = "Agent 'prometheus' is plan-mode-only; can only write to .lfe/*.md (enforced by prometheusMdOnly hook). Cannot write to team mailbox. Use delegate-task with subagent_type: 'plan' instead.",
        },
        ["sisyphus-junior"] = new() { Verdict = AgentEligibilityVerdict.Eligible },
    };

    public static Func<object?, ITeamMember> CreateParseMember(
        Func<object?, SafeParseResult<ITeamMember>> memberSchema,
        IReadOnlyDictionary<string, AgentEligibilityEntry> agentEligibilityRegistry)
    {
        return input => ParseMemberCore(input, memberSchema, agentEligibilityRegistry);
    }

    public static ITeamMember ParseMember(object? input)
    {
        return ParseMemberCore(input, ParseMemberSchema, AgentEligibilityRegistry);
    }

    private static ITeamMember ParseMemberCore(
        object? input,
        Func<object?, SafeParseResult<ITeamMember>> memberSchema,
        IReadOnlyDictionary<string, AgentEligibilityEntry> agentEligibilityRegistry)
    {
        var element = JsonHelpers.ToElement(input);
        if (element.ValueKind == JsonValueKind.Object)
        {
            var subagentType = JsonHelpers.GetString(element, "subagent_type") ?? JsonHelpers.GetString(element, "subagentType");
            if (subagentType is not null && agentEligibilityRegistry.TryGetValue(subagentType, out var eligibility) && eligibility.Verdict == AgentEligibilityVerdict.HardReject)
            {
                throw new MemberValidationError(eligibility.RejectionMessage ?? $"Agent '{subagentType}' is not eligible as a team member.");
            }
        }

        var parsed = memberSchema(input);
        return parsed.Data ?? throw new MemberValidationError(parsed.Error?.Message ?? "Member validation failed.");
    }

    private static ITeamMember TranslateFallback(
        object? input,
        IReadOnlyDictionary<string, AgentEligibilityEntry> agentEligibilityRegistry,
        Func<object?, SafeParseResult<ITeamMember>> memberSchema)
    {
        var parsed = memberSchema(input);
        if (parsed.Success)
        {
            return parsed.Data!;
        }

        throw new MemberValidationError(parsed.Error?.Message ?? "Member validation failed.");
    }

    internal static SafeParseResult<ITeamMember> ParseMemberSchema(object? input)
    {
        try
        {
            return new(true, ParseMemberDto(input), null);
        }
        catch (SchemaValidationException exception)
        {
            return new(false, default, new ValidationError(exception.Issues, exception.Message));
        }
    }

    private static ITeamMember ParseMemberDto(object? input)
    {
        var element = JsonHelpers.ToElement(input);
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new SchemaValidationException("Member must be an object", [new ValidationIssue("<root>", "Member must be an object")]);
        }

        var issues = new List<ValidationIssue>();
        var name = JsonHelpers.GetString(element, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            issues.Add(new ValidationIssue("name", "Member name is required."));
        }
        else if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9-]+$"))
        {
            issues.Add(new ValidationIssue("name", "Member name must match ^[a-z0-9-]+$."));
        }

        var kind = JsonHelpers.GetString(element, "kind");
        var category = JsonHelpers.GetString(element, "category");
        var subagentType = JsonHelpers.GetString(element, "subagent_type") ?? JsonHelpers.GetString(element, "subagentType");
        if (kind is null)
        {
            if (category is not null)
            {
                kind = "category";
            }
            else if (subagentType is not null)
            {
                kind = "subagent_type";
            }
        }

        if (kind is null)
        {
            issues.Add(new ValidationIssue("kind", "Member is missing the kind discriminator."));
        }

        if (category is not null && subagentType is not null)
        {
            issues.Add(new ValidationIssue("kind", "Member specifies both category and subagent_type."));
        }

        var cwd = JsonHelpers.GetString(element, "cwd");
        var worktreePath = JsonHelpers.GetString(element, "worktreePath") ?? JsonHelpers.GetString(element, "worktree_path");
        var subscriptions = JsonHelpers.GetStringList(element, "subscriptions");
        var backendType = JsonHelpers.GetString(element, "backendType") ?? JsonHelpers.GetString(element, "backend_type") ?? "in-process";
        var color = JsonHelpers.GetString(element, "color");
        var isActive = JsonHelpers.GetBool(element, "isActive") ?? JsonHelpers.GetBool(element, "is_active") ?? true;
        var prompt = JsonHelpers.GetString(element, "prompt") ?? JsonHelpers.GetString(element, "systemPrompt") ?? JsonHelpers.GetString(element, "system_prompt");

        if (kind == "category")
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                issues.Add(new ValidationIssue("category", "Category members require category."));
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                issues.Add(new ValidationIssue("prompt", "Category members require prompt."));
            }
        }

        if (kind == "subagent_type")
        {
            if (string.IsNullOrWhiteSpace(subagentType))
            {
                issues.Add(new ValidationIssue("subagent_type", "subagent_type is required."));
            }
        }

        if (issues.Count > 0)
        {
            throw new SchemaValidationException($"Member validation failed: {issues[0].Message}", issues);
        }

        return kind == "category"
            ? new CategoryMember
            {
                Name = name!,
                Kind = "category",
                Cwd = cwd,
                WorktreePath = worktreePath,
                Subscriptions = subscriptions,
                BackendType = backendType,
                Color = color,
                IsActive = isActive,
                Category = category!,
                Prompt = prompt!,
            }
            : new SubagentMember
            {
                Name = name!,
                Kind = "subagent_type",
                Cwd = cwd,
                WorktreePath = worktreePath,
                Subscriptions = subscriptions,
                BackendType = backendType,
                Color = color,
                IsActive = isActive,
                SubagentType = subagentType!,
                Prompt = prompt,
            };
    }

    public static MemberValidationError CreateUnknownSubagentError(string name, string memberName)
    {
        return new MemberValidationError(
            $"Unknown subagent_type '{name}'. Available ELIGIBLE agents: sisyphus, atlas, sisyphus-junior, hephaestus (if D-36 applied). Use delegate-task for read-only agents like oracle, librarian, explore, metis, momus, multimodal-looker.",
            memberName,
            "unknown-subagent");
    }
}

internal sealed class SchemaValidationException : Exception
{
    public SchemaValidationException(string message, IReadOnlyList<ValidationIssue> issues)
        : base(message)
    {
        Issues = issues;
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }
}
