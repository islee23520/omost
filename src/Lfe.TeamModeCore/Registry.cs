using System.Text.Json;

namespace Lfe.TeamModeCore;

public sealed class TeamSpecValidationError : Exception
{
    public TeamSpecValidationError(string message, string code, string? field = null, string? memberName = null)
        : base(message)
    {
        Code = code;
        Field = field;
        MemberName = memberName;
    }

    public string Code { get; }

    public string? Field { get; }

    public string? MemberName { get; }
}

public static class TeamSpecRegistry
{
    private const int MaxTeamMembers = 8;

    private static readonly string[] HyperplanRequiredCategories = ["unspecified-low", "unspecified-high", "ultrabrain", "artistry"];

    private const string UnknownSubagentMessage = "Unknown subagent_type '<name>'. Available ELIGIBLE agents: sisyphus, atlas, sisyphus-junior, hephaestus (if D-36 applied). Use delegate-task for read-only agents like oracle, librarian, explore, metis, momus, multimodal-looker.";

    public static string NormalizeNameStem(string value) => JsonHelpers.NormalizeNameStem(value);

    public static object? NormalizeTeamSpecInput(object? raw, NormalizeTeamSpecInputOptions? options = null)
    {
        if (raw is null)
        {
            return null;
        }

        var rawObject = JsonHelpers.ToPlainObject(raw);
        if (rawObject is null)
        {
            return raw;
        }

        var normalizedSpec = new Dictionary<string, object?>(rawObject, StringComparer.Ordinal);
        if (normalizedSpec.TryGetValue("name", out var nameValue) && nameValue is string nameText)
        {
            normalizedSpec["name"] = NormalizeNameStem(nameText);
        }

        normalizedSpec.Remove("lead");

        var rawMembers = rawObject.TryGetValue("members", out var membersValue) && membersValue is List<object?> list ? list : null;
        var rawLead = rawObject.TryGetValue("lead", out var leadValue) ? leadValue : null;
        var leadAgentId = rawObject.TryGetValue("leadAgentId", out var leadIdValue) && leadIdValue is string leadIdText ? leadIdText : null;
        var hasExplicitLead = leadAgentId is not null || rawLead is Dictionary<string, object?> || (rawMembers?.Any(member => member is Dictionary<string, object?> dict && dict.ContainsKey("isLead") && dict["isLead"] is true) ?? false);

        if (rawMembers is not null)
        {
            var normalizedMembers = rawMembers.Select(member => member is Dictionary<string, object?> dict ? (object?)NormalizeInlineMember(dict, options) : member).ToList();
            var callerTeamLead = options?.CallerTeamLead;
            var shouldUseFirstMemberAsLead = !hasExplicitLead && normalizedMembers.Count >= MaxTeamMembers && callerTeamLead?.IsEligibleForTeamLead == true;

            if (rawLead is Dictionary<string, object?> leadObject)
            {
                var leadMember = NormalizeInlineMember(leadObject, options);
                if (!leadMember.ContainsKey("name"))
                {
                    leadMember["name"] = "lead";
                }

                var leadName = JsonHelpers.GetMemberName(leadMember);
                var alreadyPresent = leadName is not null && normalizedMembers.Any(member => JsonHelpers.GetMemberName(member) == leadName);
                if (!alreadyPresent)
                {
                    normalizedMembers.Insert(0, leadMember);
                }

                if (leadAgentId is null && leadName is not null)
                {
                    leadAgentId = leadName;
                }
            }

            if (shouldUseFirstMemberAsLead)
            {
                leadAgentId = JsonHelpers.GetMemberName(normalizedMembers[0]);
            }
            else if (!hasExplicitLead)
            {
                if (callerTeamLead?.IsEligibleForTeamLead == true && callerTeamLead.AgentTypeId is not null)
                {
                    normalizedMembers.Insert(0, CreateCallerLeadMember(callerTeamLead.AgentTypeId));
                    leadAgentId = "lead";
                }
                else if (callerTeamLead?.DisplayName is not null)
                {
                    throw new InvalidOperationException($"Caller agent {callerTeamLead.DisplayName} is not eligible as team lead; specify leadAgentId explicitly");
                }
            }

            normalizedMembers = AssignGeneratedMemberNames(normalizedMembers, options);

            if (leadAgentId is null && shouldUseFirstMemberAsLead)
            {
                leadAgentId = JsonHelpers.GetMemberName(normalizedMembers[0]);
            }

            normalizedMembers = normalizedMembers.Select(member =>
            {
                var memberName = JsonHelpers.GetMemberName(member);
                if (leadAgentId is null && member is Dictionary<string, object?> memberDict && memberDict.TryGetValue("isLead", out var isLeadValue) && isLeadValue is true && memberName is not null)
                {
                    leadAgentId = memberName;
                }

                return member is Dictionary<string, object?> stripCandidate ? (object?)StripMemberLeadFlag(stripCandidate) : member;
            }).ToList();

            if (leadAgentId is not null && !normalizedMembers.Any(member => JsonHelpers.GetMemberName(member) == leadAgentId))
            {
                var normalizedLeadAgentId = NormalizeNameStem(leadAgentId);
                if (normalizedMembers.Any(member => JsonHelpers.GetMemberName(member) == normalizedLeadAgentId))
                {
                    leadAgentId = normalizedLeadAgentId;
                }
            }

            if (leadAgentId is null && normalizedMembers.Count == 1)
            {
                leadAgentId = JsonHelpers.GetMemberName(normalizedMembers[0]);
            }

            normalizedSpec["members"] = normalizedMembers;
        }

        if (leadAgentId is not null)
        {
            normalizedSpec["leadAgentId"] = leadAgentId;
        }

        return normalizedSpec;
    }

    public static TeamSpec ParseTeamSpec(object? rawSpec, NormalizeTeamSpecInputOptions? options = null)
    {
        var parsedSpecResult = TeamSpecSchema.SafeParse(NormalizeTeamSpecInput(rawSpec, options));
        if (!parsedSpecResult.Success || parsedSpecResult.Data is null)
        {
            throw new TeamSpecValidationError($"Invalid team spec: {FormatTeamSpecIssues(parsedSpecResult.Error!)}", "INVALID_TEAM_SPEC");
        }

        ValidateSpec(parsedSpecResult.Data);
        return parsedSpecResult.Data;
    }

    public static string FormatTeamSpecIssues(ValidationError error)
    {
        return string.Join("; ", error.Issues.Take(5).Select(issue => $"{issue.Path}: {issue.Message}"));
    }

    public static void ValidateSpec(TeamSpec spec)
    {
        if (spec.Members.Count > MaxTeamMembers)
        {
            throw new TeamSpecValidationError($"Team '{spec.Name}' exceeds max 8 members.", "TEAM_MEMBER_LIMIT_EXCEEDED", "members");
        }

        var seenMemberNames = new HashSet<string>(StringComparer.Ordinal);
        var leadMatchCount = 0;

        foreach (var member in spec.Members)
        {
            if (!seenMemberNames.Add(member.Name))
            {
                throw new TeamSpecValidationError($"Member name '{member.Name}' is duplicated within team '{spec.Name}'. Member names must be unique.", "DUPLICATE_MEMBER_NAME", "members", member.Name);
            }

            ValidateMemberEligibility(member);
            ValidateDualSupport(member);

            if (member.Name == spec.LeadAgentId)
            {
                leadMatchCount += 1;
            }
        }

        if (leadMatchCount != 1)
        {
            throw new TeamSpecValidationError($"Team '{spec.Name}' leadAgentId '{spec.LeadAgentId}' must match exactly one member.name.", "INVALID_LEAD_AGENT_ID", "leadAgentId");
        }

        ValidateHyperplanComposition(spec);
    }

    public static void ValidateMemberEligibility(ITeamMember member)
    {
        if (member.Kind != "subagent_type")
        {
            return;
        }

        if (member.SubagentType is null || !MemberParser.AgentEligibilityRegistry.TryGetValue(member.SubagentType, out var eligibility))
        {
            throw new TeamSpecValidationError(UnknownSubagentMessage.Replace("<name>", member.SubagentType ?? string.Empty), "UNKNOWN_SUBAGENT_TYPE", "subagent_type", member.Name);
        }

        if (eligibility.Verdict == AgentEligibilityVerdict.HardReject)
        {
            throw new TeamSpecValidationError(eligibility.RejectionMessage ?? $"Agent '{member.SubagentType}' is not eligible as a team member.", "INELIGIBLE_AGENT", "subagent_type", member.Name);
        }
    }

    public static void ValidateDualSupport(ITeamMember member)
    {
        var trimmedPrompt = member.Prompt?.Trim();

        if (trimmedPrompt == string.Empty)
        {
            throw new TeamSpecValidationError($"Member '{member.Name}' prompt must not be empty after trimming whitespace.", "EMPTY_PROMPT", "prompt", member.Name);
        }

        if (member.Kind == "category" && member.Prompt is not null && member.Prompt.Trim().Length < 8)
        {
            throw new TeamSpecValidationError($"Member '{member.Name}' category prompt must be at least 8 characters long.", "CATEGORY_PROMPT_TOO_SHORT", "prompt", member.Name);
        }
    }

    private static void ValidateHyperplanComposition(TeamSpec spec)
    {
        if (!string.Equals(spec.Name, "hyperplan", StringComparison.Ordinal))
        {
            return;
        }

        var categories = new HashSet<string>(spec.Members.Where(member => member.Kind == "category" && member.Category is not null).Select(member => member.Category!), StringComparer.Ordinal);
        foreach (var category in HyperplanRequiredCategories)
        {
            if (!categories.Contains(category))
            {
                throw new TeamSpecValidationError($"Hyperplan team must include category '{category}'.", "HYPERPLAN_REQUIRED_CATEGORY_MISSING", "members");
            }
        }
    }

    private static Dictionary<string, object?> NormalizeInlineMember(Dictionary<string, object?> member, NormalizeTeamSpecInputOptions? options)
    {
        var normalizedMember = new Dictionary<string, object?>(member, StringComparer.Ordinal);

        normalizedMember.Remove("capabilities");
        normalizedMember.Remove("description");
        normalizedMember.Remove("loadSkills");
        normalizedMember.Remove("load_skills");
        normalizedMember.Remove("permission");
        normalizedMember.Remove("responsibilities");
        normalizedMember.Remove("role");
        normalizedMember.Remove("systemPrompt");
        normalizedMember.Remove("system_prompt");

        var rawKind = normalizedMember.TryGetValue("kind", out var kindValue) ? kindValue : null;

        if (!normalizedMember.ContainsKey("kind"))
        {
            if (normalizedMember.TryGetValue("category", out var categoryValue) && categoryValue is string)
            {
                normalizedMember["kind"] = "category";
            }
            else if (normalizedMember.TryGetValue("subagent_type", out var subagentValue) && subagentValue is string)
            {
                normalizedMember["kind"] = "subagent_type";
            }
            else if (options?.DefaultCategoryName is not null)
            {
                normalizedMember["kind"] = "category";
                normalizedMember["category"] = options.DefaultCategoryName;
            }
        }
        else if (rawKind is string kindText && kindText != "category" && kindText != "subagent_type")
        {
            if (normalizedMember.TryGetValue("category", out var categoryValue) && categoryValue is string)
            {
                normalizedMember["kind"] = "category";
            }
            else if (normalizedMember.TryGetValue("subagent_type", out var subagentValue) && subagentValue is string)
            {
                normalizedMember["kind"] = "subagent_type";
            }
            else if (kindText is not "agent" and not "member" and not "worker" and not "analyst")
            {
                normalizedMember["kind"] = "category";
                normalizedMember["category"] = kindText;
            }
            else if (options?.DefaultCategoryName is not null)
            {
                normalizedMember["kind"] = "category";
                normalizedMember["category"] = options.DefaultCategoryName;
            }
        }

        if (normalizedMember.TryGetValue("kind", out var kind) && kind as string == "category" && !normalizedMember.ContainsKey("prompt"))
        {
            normalizedMember["prompt"] = BuildPromptFromNaturalMember(member);
        }

        return normalizedMember;
    }

    private static string BuildPromptFromNaturalMember(Dictionary<string, object?> member)
    {
        if (GetPromptAlias(member) is string promptAlias)
        {
            return promptAlias;
        }

        var promptParts = new List<string>();
        if (member.TryGetValue("role", out var roleValue) && roleValue is string role)
        {
            promptParts.Add($"Role: {role}");
        }

        if (member.TryGetValue("description", out var descriptionValue) && descriptionValue is string description)
        {
            promptParts.Add(description);
        }

        var capabilities = FormatStringArray(member.TryGetValue("capabilities", out var capabilitiesValue) ? capabilitiesValue : null);
        if (capabilities is not null)
        {
            promptParts.Add(capabilities);
        }

        var responsibilities = FormatStringArray(member.TryGetValue("responsibilities", out var responsibilitiesValue) ? responsibilitiesValue : null);
        if (responsibilities is not null)
        {
            promptParts.Add(responsibilities);
        }

        return promptParts.Count > 0 ? string.Join("\n", promptParts) : "Work on the assigned team task and report findings to the lead.";
    }

    private static string? GetPromptAlias(Dictionary<string, object?> member)
    {
        return member.TryGetValue("prompt", out var promptValue) && promptValue is string prompt ? prompt
            : member.TryGetValue("systemPrompt", out var systemPromptValue) && systemPromptValue is string systemPrompt ? systemPrompt
            : member.TryGetValue("system_prompt", out var snakeSystemPromptValue) && snakeSystemPromptValue is string snakeSystemPrompt ? snakeSystemPrompt
            : null;
    }

    private static string? FormatStringArray(object? value)
    {
        if (value is not IEnumerable<object?> list)
        {
            return null;
        }

        var strings = list.OfType<string>().Where(item => item.Trim().Length > 0).ToList();
        return strings.Count > 0 ? string.Join(", ", strings) : null;
    }

    private static List<object?> AssignGeneratedMemberNames(List<object?> rawMembers, NormalizeTeamSpecInputOptions? options)
    {
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<object?>();

        foreach (var rawMember in rawMembers)
        {
            if (rawMember is not Dictionary<string, object?> member)
            {
                continue;
            }

            var rawName = JsonHelpers.GetMemberName(member);
            var stem = rawName is null ? DeriveMemberNameStem(member) : NormalizeNameStem(rawName);
            var generatedName = rawName is null ? $"{stem}-1" : stem;
            var suffix = rawName is null ? 1 : 2;
            while (usedNames.Contains(generatedName))
            {
                generatedName = $"{stem}-{suffix}";
                suffix += 1;
            }

            usedNames.Add(generatedName);
            member["name"] = generatedName;
            result.Add(member);
        }

        return result;
    }

    private static string DeriveMemberNameStem(Dictionary<string, object?> member)
    {
        if (member.TryGetValue("kind", out var kindValue) && kindValue is string kind)
        {
            if (kind == "category" && member.TryGetValue("category", out var categoryValue) && categoryValue is string category)
            {
                return NormalizeNameStem(category);
            }

            if (kind == "subagent_type" && member.TryGetValue("subagent_type", out var subagentValue) && subagentValue is string subagent)
            {
                return NormalizeNameStem(subagent);
            }
        }

        return "member";
    }

    private static Dictionary<string, object?> CreateCallerLeadMember(string callerAgentTypeId)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = "lead",
            ["kind"] = "subagent_type",
            ["subagent_type"] = callerAgentTypeId,
        };
    }

    private static Dictionary<string, object?> StripMemberLeadFlag(Dictionary<string, object?> value)
    {
        if (!value.ContainsKey("isLead"))
        {
            return value;
        }

        var copy = new Dictionary<string, object?>(value, StringComparer.Ordinal);
        copy.Remove("isLead");
        return copy;
    }
}

public static class TeamSpecSchema
{
    public static TeamSpec Parse(object? input)
    {
        var result = SafeParse(input);
        if (!result.Success || result.Data is null)
        {
            throw new SchemaValidationException(result.Error?.Message ?? "Invalid team spec.", result.Error?.Issues ?? []);
        }

        return result.Data;
    }

    public static SafeParseResult<TeamSpec> SafeParse(object? input)
    {
        try
        {
            return new(true, ParseTeamSpecDto(input), null);
        }
        catch (SchemaValidationException exception)
        {
            return new(false, default, new ValidationError(exception.Issues, exception.Message));
        }
    }

    private static TeamSpec ParseTeamSpecDto(object? input)
    {
        var element = JsonHelpers.ToElement(input);
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new SchemaValidationException("Team spec must be an object", [new ValidationIssue("<root>", "Team spec must be an object")]);
        }

        var issues = new List<ValidationIssue>();
        var version = (int)(JsonHelpers.GetLong(element, "version") ?? 1);
        var name = JsonHelpers.GetString(element, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            issues.Add(new ValidationIssue("name", "Team name is required."));
        }
        else if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9-]+$"))
        {
            issues.Add(new ValidationIssue("name", "Team name must match ^[a-z0-9-]+$."));
        }

        var membersElementExists = JsonHelpers.TryGetProperty(element, "members", out var membersElement);
        if (!membersElementExists || membersElement.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue("members", "Team members are required."));
        }

        var leadAgentId = JsonHelpers.GetString(element, "leadAgentId");
        var description = JsonHelpers.GetString(element, "description");
        var createdAt = JsonHelpers.GetLong(element, "createdAt") ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var teamAllowedPaths = JsonHelpers.GetStringList(element, "teamAllowedPaths");
        var sessionPermission = JsonHelpers.GetString(element, "sessionPermission");

        var members = new List<ITeamMember>();
        if (membersElementExists && membersElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var memberElement in membersElement.EnumerateArray())
            {
                members.Add(MemberParser.ParseMemberSchema(memberElement).Data ?? throw new SchemaValidationException("Invalid team member.", [new ValidationIssue("members", "Invalid team member.")]));
            }
        }

        if (members.Count < 1 || members.Count > 8)
        {
            issues.Add(new ValidationIssue("members", "Team members must contain between 1 and 8 entries."));
        }

        if (leadAgentId is null && members.Count > 1)
        {
            issues.Add(new ValidationIssue("leadAgentId", "leadAgentId required (or write a `lead: {...}` field, or mark one member with `isLead: true`)"));
        }

        if (issues.Count > 0)
        {
            throw new SchemaValidationException($"Team spec validation failed: {issues[0].Message}", issues);
        }

        if (leadAgentId is null && members.Count == 1)
        {
            leadAgentId = members[0].Name;
        }

        return new TeamSpec
        {
            Version = version,
            Name = name!,
            Description = description,
            CreatedAt = createdAt,
            LeadAgentId = leadAgentId,
            TeamAllowedPaths = teamAllowedPaths,
            SessionPermission = sessionPermission,
            Members = members,
        };
    }
}
