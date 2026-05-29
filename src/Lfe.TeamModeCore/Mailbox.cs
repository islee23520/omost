using System.Text;

namespace Lfe.TeamModeCore;

public sealed class BroadcastNotPermittedError(string message = "broadcast requires lead role") : Exception(message);

public sealed class PayloadTooLargeError(string message = "payload exceeds 32 KB") : Exception(message);

public sealed class RecipientBackpressureError(string message = "recipient inbox full (backpressure)") : Exception(message);

public sealed class DuplicateMessageIdError(string message = "duplicate message id") : Exception(message);

public sealed class TeamDeletingError(string message = "team is deleting") : Exception(message);

public static class Mailbox
{
    public static Dictionary<string, MailboxEntry> CreateEmptyMailboxState(IEnumerable<string>? memberNames = null)
    {
        return (memberNames ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).ToDictionary(memberName => memberName, _ => new MailboxEntry(), StringComparer.Ordinal);
    }

    private static MailboxEntry CloneMailboxEntry(MailboxEntry? entry)
    {
        return new MailboxEntry
        {
            Unread = [.. (entry?.Unread ?? [])],
            Reserved = [.. (entry?.Reserved ?? [])],
            Processed = [.. (entry?.Processed ?? [])],
        };
    }

    private static Dictionary<string, MailboxEntry> CloneMailboxState(Dictionary<string, MailboxEntry> state)
    {
        return state.ToDictionary(entry => entry.Key, entry => CloneMailboxEntry(entry.Value), StringComparer.Ordinal);
    }

    private static MailboxEntry GetOrCreateEntry(Dictionary<string, MailboxEntry> state, string memberName)
    {
        if (state.TryGetValue(memberName, out var entry))
        {
            return entry;
        }

        var createdEntry = new MailboxEntry();
        state[memberName] = createdEntry;
        return createdEntry;
    }

    private static int UnreadBytes(MailboxEntry entry)
    {
        return entry.Unread.Concat(entry.Reserved).Sum(message => Encoding.UTF8.GetByteCount($"{System.Text.Json.JsonSerializer.Serialize(message, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}\n"));
    }

    public static void AssertTeamAcceptsMessages(RuntimeState runtimeState)
    {
        if (runtimeState.Status is "deleting" or "deleted")
        {
            throw new TeamDeletingError();
        }
    }

    public static List<string> ResolveMessageRecipients(Message message, SendContext context)
    {
        return message.To != "*" ? [message.To] : context.ActiveMembers.Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool HasMessageId(MailboxEntry entry, string messageId)
    {
        return entry.Unread.Concat(entry.Reserved).Any(message => message.MessageId == messageId);
    }

    public static SendMessageResult SendMessageToState(
        Dictionary<string, MailboxEntry> state,
        Message message,
        SendContext context,
        SendMessageLimits limits,
        RuntimeState? runtimeState = null)
    {
        var parsedMessage = MessageSchema.Parse(message);
        var payloadBytes = Encoding.UTF8.GetByteCount(parsedMessage.Body);
        if (payloadBytes > limits.MessagePayloadMaxBytes)
        {
            throw new PayloadTooLargeError();
        }

        if (runtimeState is not null)
        {
            AssertTeamAcceptsMessages(runtimeState);
        }

        if (parsedMessage.To == "*" && !context.IsLead)
        {
            throw new BroadcastNotPermittedError();
        }

        var nextState = CloneMailboxState(state);
        var deliveredTo = new List<string>();
        var serializedMessageBytes = Encoding.UTF8.GetByteCount($"{System.Text.Json.JsonSerializer.Serialize(parsedMessage, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}\n");

        foreach (var recipient in ResolveMessageRecipients(parsedMessage, context))
        {
            var entry = GetOrCreateEntry(nextState, recipient);
            if (UnreadBytes(entry) + serializedMessageBytes > limits.RecipientUnreadMaxBytes)
            {
                throw new RecipientBackpressureError();
            }

            if (HasMessageId(entry, parsedMessage.MessageId))
            {
                throw new DuplicateMessageIdError();
            }

            if (context.ReservedRecipients?.Contains(recipient) == true)
            {
                entry.Reserved.Add(parsedMessage);
            }
            else
            {
                entry.Unread.Add(parsedMessage);
            }

            deliveredTo.Add(recipient);
        }

        return new SendMessageResult { State = nextState, MessageId = parsedMessage.MessageId, DeliveredTo = deliveredTo };
    }

    public static List<Message> ListUnreadMessagesFromState(Dictionary<string, MailboxEntry> state, string memberName)
    {
        return state.TryGetValue(memberName, out var entry)
            ? entry.Unread.OrderBy(message => message.Timestamp).ToList()
            : [];
    }

    public static (Dictionary<string, MailboxEntry> State, DeliveryReservation? Reservation) ReserveMessageForDeliveryInState(Dictionary<string, MailboxEntry> state, string memberName, string messageId)
    {
        var nextState = CloneMailboxState(state);
        var entry = GetOrCreateEntry(nextState, memberName);

        if (entry.Reserved.Any(message => message.MessageId == messageId))
        {
            return (nextState, new DeliveryReservation { MemberName = memberName, MessageId = messageId });
        }

        var messageIndex = entry.Unread.FindIndex(message => message.MessageId == messageId);
        if (messageIndex < 0)
        {
            return (nextState, null);
        }

        var message = entry.Unread[messageIndex];
        entry.Unread = entry.Unread.Where(candidate => candidate.MessageId != messageId).ToList();
        entry.Reserved.Add(message);
        return (nextState, new DeliveryReservation { MemberName = memberName, MessageId = messageId });
    }

    public static Dictionary<string, MailboxEntry> CommitDeliveryReservationInState(Dictionary<string, MailboxEntry> state, DeliveryReservation reservation)
    {
        var nextState = CloneMailboxState(state);
        var entry = GetOrCreateEntry(nextState, reservation.MemberName);
        var message = entry.Reserved.FirstOrDefault(candidate => candidate.MessageId == reservation.MessageId);
        if (message is null) return nextState;

        entry.Reserved = entry.Reserved.Where(candidate => candidate.MessageId != reservation.MessageId).ToList();
        entry.Processed.Add(message);
        return nextState;
    }

    public static Dictionary<string, MailboxEntry> ReleaseDeliveryReservationInState(Dictionary<string, MailboxEntry> state, DeliveryReservation reservation)
    {
        var nextState = CloneMailboxState(state);
        var entry = GetOrCreateEntry(nextState, reservation.MemberName);
        var message = entry.Reserved.FirstOrDefault(candidate => candidate.MessageId == reservation.MessageId);
        if (message is null) return nextState;

        entry.Reserved = entry.Reserved.Where(candidate => candidate.MessageId != reservation.MessageId).ToList();
        entry.Unread.Add(message);
        return nextState;
    }

    public static Dictionary<string, MailboxEntry> AckMessagesInState(Dictionary<string, MailboxEntry> state, string memberName, IEnumerable<string> messageIds)
    {
        var nextState = CloneMailboxState(state);
        var entry = GetOrCreateEntry(nextState, memberName);
        foreach (var messageId in messageIds)
        {
            var unreadMessage = entry.Unread.FirstOrDefault(message => message.MessageId == messageId);
            var reservedMessage = entry.Reserved.FirstOrDefault(message => message.MessageId == messageId);
            var message = unreadMessage ?? reservedMessage;
            if (message is null)
            {
                continue;
            }

            entry.Unread = entry.Unread.Where(candidate => candidate.MessageId != messageId).ToList();
            entry.Reserved = entry.Reserved.Where(candidate => candidate.MessageId != messageId).ToList();
            entry.Processed.Add(message);
            nextState[memberName] = entry;
        }

        return nextState;
    }

    private static string EscapeAttributeValue(string value)
    {
        return value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;");
    }

    public static string BuildEnvelope(Message message)
    {
        var attributes = new List<string>
        {
            $"from=\"{EscapeAttributeValue(message.From)}\"",
            $"timestamp=\"{EscapeAttributeValue(message.Timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture))}\"",
            $"messageId=\"{EscapeAttributeValue(message.MessageId)}\"",
            $"kind=\"{EscapeAttributeValue(message.Kind)}\"",
            $"correlationId=\"{EscapeAttributeValue(message.CorrelationId ?? string.Empty)}\"",
        };

        if (message.Summary is not null)
        {
            attributes.Add($"summary=\"{EscapeAttributeValue(message.Summary)}\"");
        }

        if (message.References is not null)
        {
            attributes.Add($"references=\"{EscapeAttributeValue(System.Text.Json.JsonSerializer.Serialize(message.References))}\"");
        }

        return $"<peer_message {string.Join(" ", attributes)}>\n{message.Body}\n</peer_message>";
    }

    public static PollInjectionResult PollAndBuildInjectionFromState(RuntimeState runtimeState, Dictionary<string, MailboxEntry> mailboxState, string memberName, string turnMarker)
    {
        var runtimeMember = runtimeState.Members.FirstOrDefault(member => member.Name == memberName);
        if (runtimeMember is null)
        {
            throw new InvalidOperationException($"runtime member not found: {memberName}");
        }

        if (runtimeMember.LastInjectedTurnMarker == turnMarker)
        {
            return new PollInjectionResult { RuntimeState = runtimeState, Injected = false, MessageIds = [], Reason = "already injected this turn" };
        }

        var pendingMessageIds = new HashSet<string>(runtimeMember.PendingInjectedMessageIds, StringComparer.Ordinal);
        var injectableMessages = ListUnreadMessagesFromState(mailboxState, memberName).Where(message => !pendingMessageIds.Contains(message.MessageId)).ToList();

        if (injectableMessages.Count == 0)
        {
            return new PollInjectionResult
            {
                RuntimeState = runtimeState,
                Injected = false,
                MessageIds = [],
                Reason = pendingMessageIds.Count > 0 ? "pending ack" : "no unread",
            };
        }

        var messageIds = injectableMessages.Select(message => message.MessageId).ToList();
        var content = string.Join("\n", injectableMessages.Select(BuildEnvelope));
        return new PollInjectionResult
        {
            RuntimeState = RuntimeStateManager.MarkMessagesPendingForMember(runtimeState, memberName, messageIds, turnMarker),
            Injected = true,
            Content = content,
            MessageIds = messageIds,
        };
    }
}
