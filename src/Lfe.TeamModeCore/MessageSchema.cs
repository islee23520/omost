using System.Text.Json;

namespace Lfe.TeamModeCore;

public static class MessageSchema
{
    public static Message Parse(object? input)
    {
        var result = SafeParse(input);
        if (!result.Success || result.Data is null)
        {
            throw new SchemaValidationException(result.Error?.Message ?? "Invalid message.", result.Error?.Issues ?? []);
        }

        return result.Data;
    }

    public static SafeParseResult<Message> SafeParse(object? input)
    {
        try
        {
            return new(true, ParseMessageDto(input), null);
        }
        catch (SchemaValidationException exception)
        {
            return new(false, default, new ValidationError(exception.Issues, exception.Message));
        }
    }

    private static Message ParseMessageDto(object? input)
    {
        var element = JsonHelpers.ToElement(input);
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new SchemaValidationException("Message must be an object", [new ValidationIssue("<root>", "Message must be an object")]);
        }

        var issues = new List<ValidationIssue>();
        var messageId = JsonHelpers.GetString(element, "messageId");
        var from = JsonHelpers.GetString(element, "from");
        var to = JsonHelpers.GetString(element, "to");
        var kind = JsonHelpers.GetString(element, "kind");
        var body = JsonHelpers.GetString(element, "body");
        var timestamp = JsonHelpers.GetLong(element, "timestamp");

        if (string.IsNullOrWhiteSpace(messageId)) issues.Add(new ValidationIssue("messageId", "messageId is required."));
        if (string.IsNullOrWhiteSpace(from)) issues.Add(new ValidationIssue("from", "from is required."));
        if (string.IsNullOrWhiteSpace(to)) issues.Add(new ValidationIssue("to", "to is required."));
        if (string.IsNullOrWhiteSpace(kind) || !new[] { "message", "shutdown_request", "shutdown_approved", "shutdown_rejected", "announcement" }.Contains(kind, StringComparer.Ordinal)) issues.Add(new ValidationIssue("kind", "Invalid message kind."));
        if (body is null || body.Length > 32 * 1024) issues.Add(new ValidationIssue("body", "body is required and must be <= 32 KB."));
        if (timestamp is null or <= 0) issues.Add(new ValidationIssue("timestamp", "timestamp must be positive."));

        if (issues.Count > 0)
        {
            throw new SchemaValidationException($"Message validation failed: {issues[0].Message}", issues);
        }

        return new Message
        {
            Version = (int)(JsonHelpers.GetLong(element, "version") ?? 1),
            MessageId = messageId!,
            From = from!,
            To = to!,
            Kind = kind!,
            Body = body!,
            Summary = JsonHelpers.GetString(element, "summary"),
            References = JsonHelpers.GetObjectList(element, "references")?.Select(reference => new TeamReference
            {
                Path = reference is Dictionary<string, object?> dict && dict.TryGetValue("path", out var path) && path is string pathText ? pathText : string.Empty,
                Description = reference is Dictionary<string, object?> referenceDict && referenceDict.TryGetValue("description", out var description) && description is string descriptionText ? descriptionText : null,
            }).ToList(),
            Timestamp = timestamp!.Value,
            CorrelationId = JsonHelpers.GetString(element, "correlationId"),
            Color = JsonHelpers.GetString(element, "color"),
        };
    }
}
