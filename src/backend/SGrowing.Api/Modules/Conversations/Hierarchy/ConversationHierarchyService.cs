using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Conversations.Hierarchy;

#region Enums

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HierarchyRelationshipType
{
    ParentChild,
    SplitChild,
    MergedSource,
    BlockedBy,
    Related
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SplitReason
{
    MultipleInquiries,
    DepartmentTransfer,
    EscalatedComponent,
    InternalInvestigation
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MergeReason
{
    DuplicateTicket,
    FollowUpMessage,
    MultiChannelSameCustomer,
    ChannelReopen
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HierarchyConversationStatus
{
    Open,
    Pending,
    Resolved,
    Closed,
    Merged
}

#endregion

#region Models & DTOs

public record HierarchyMessageRecord
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string Sender { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public DateTime SentAtUtc { get; init; } = DateTime.UtcNow;
    public bool IsSplitMoved { get; set; }
}

public record HierarchyConversationRecord
{
    public Guid ConversationId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string Title { get; set; } = string.Empty;
    public HierarchyConversationStatus Status { get; set; } = HierarchyConversationStatus.Open;
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? Channel { get; init; }
    public Guid? AssignedAgentId { get; set; }
    public string? Department { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? SplitFromConversationId { get; set; }
    public Guid? MergedIntoConversationId { get; set; }
    public List<HierarchyMessageRecord> Messages { get; init; } = new();
    public List<string> AuditTrail { get; init; } = new();
}

public record ConversationLinkRecord(
    Guid LinkId,
    Guid TenantId,
    Guid ParentConversationId,
    Guid ChildConversationId,
    HierarchyRelationshipType Relationship,
    bool IsBlocking,
    DateTime LinkedAtUtc
);

public record SplitConversationRequest(
    Guid TenantId,
    Guid SourceConversationId,
    List<Guid> MessageIdsToMove,
    string NewTitle,
    Guid? AssignedAgentId = null,
    string? Department = null,
    SplitReason Reason = SplitReason.MultipleInquiries,
    bool CopyCustomerContext = true
);

public record SplitConversationResult(
    Guid SourceConversationId,
    Guid NewChildConversationId,
    int MovedMessagesCount,
    DateTime SplitAtUtc
);

public record MergeConversationsRequest(
    Guid TenantId,
    Guid TargetConversationId,
    List<Guid> SourceConversationIds,
    MergeReason Reason,
    string? MergeNote = null,
    bool CloseSourceConversations = true
);

public record MergeConversationsResult(
    Guid TargetConversationId,
    List<Guid> MergedSourceIds,
    int ReconciledMessagesCount,
    DateTime MergedAtUtc
);

public record ConversationHierarchyNode(
    Guid ConversationId,
    string Title,
    HierarchyConversationStatus Status,
    HierarchyRelationshipType Relationship,
    bool IsBlocking,
    List<ConversationHierarchyNode> Children
);

public record HierarchyTreeSummary(
    Guid RootConversationId,
    int TotalDescendants,
    int OpenCount,
    int ResolvedCount,
    bool CanParentClose,
    List<string> BlockingReasons
);

#endregion

public interface IConversationHierarchyService
{
    Task<HierarchyConversationRecord> RegisterConversationAsync(HierarchyConversationRecord conv, CancellationToken ct = default);
    Task<HierarchyConversationRecord?> GetConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);

    Task<SplitConversationResult> SplitConversationAsync(SplitConversationRequest request, CancellationToken ct = default);
    Task<MergeConversationsResult> MergeConversationsAsync(MergeConversationsRequest request, CancellationToken ct = default);

    Task<ConversationLinkRecord> LinkConversationsAsync(Guid tenantId, Guid parentId, Guid childId, HierarchyRelationshipType relationship, bool isBlocking = false, CancellationToken ct = default);
    Task<bool> UnlinkConversationsAsync(Guid tenantId, Guid parentId, Guid childId, CancellationToken ct = default);

    Task<ConversationHierarchyNode> GetHierarchyTreeAsync(Guid tenantId, Guid rootConversationId, CancellationToken ct = default);
    Task<HierarchyTreeSummary> CanCloseConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
}

public class ConversationHierarchyService : IConversationHierarchyService
{
    private readonly ConcurrentDictionary<Guid, HierarchyConversationRecord> _conversations = new();
    private readonly ConcurrentDictionary<Guid, ConversationLinkRecord> _links = new();

    public Task<HierarchyConversationRecord> RegisterConversationAsync(HierarchyConversationRecord conv, CancellationToken ct = default)
    {
        _conversations[conv.ConversationId] = conv;
        return Task.FromResult(conv);
    }

    public Task<HierarchyConversationRecord?> GetConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        if (_conversations.TryGetValue(conversationId, out var conv) && conv.TenantId == tenantId)
        {
            return Task.FromResult<HierarchyConversationRecord?>(conv);
        }
        return Task.FromResult<HierarchyConversationRecord?>(null);
    }

    public Task<SplitConversationResult> SplitConversationAsync(SplitConversationRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (request.SourceConversationId == Guid.Empty) throw new ArgumentException("SourceConversationId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.NewTitle)) throw new ArgumentException("NewTitle is required.", nameof(request));

        if (!_conversations.TryGetValue(request.SourceConversationId, out var source) || source.TenantId != request.TenantId)
        {
            throw new KeyNotFoundException($"Source conversation {request.SourceConversationId} not found.");
        }

        var messagesToMove = source.Messages
            .Where(m => request.MessageIdsToMove.Contains(m.MessageId) && !m.IsSplitMoved)
            .ToList();

        if (messagesToMove.Count == 0 && request.MessageIdsToMove.Count > 0)
        {
            throw new InvalidOperationException("None of the specified messages are available to split.");
        }

        var now = DateTime.UtcNow;
        var child = new HierarchyConversationRecord
        {
            TenantId = request.TenantId,
            Title = request.NewTitle.Trim(),
            Status = HierarchyConversationStatus.Open,
            CustomerId = request.CopyCustomerContext ? source.CustomerId : null,
            CustomerName = request.CopyCustomerContext ? source.CustomerName : null,
            Channel = source.Channel,
            AssignedAgentId = request.AssignedAgentId ?? source.AssignedAgentId,
            Department = request.Department ?? source.Department,
            ParentId = source.ConversationId,
            SplitFromConversationId = source.ConversationId
        };

        foreach (var msg in messagesToMove)
        {
            msg.IsSplitMoved = true;
            child.Messages.Add(new HierarchyMessageRecord
            {
                MessageId = Guid.NewGuid(),
                ConversationId = child.ConversationId,
                Sender = msg.Sender,
                Text = msg.Text,
                SentAtUtc = msg.SentAtUtc,
                IsSplitMoved = false
            });
        }

        child.AuditTrail.Add($"Created via Split from ticket #{source.ConversationId}. Reason: {request.Reason}. {messagesToMove.Count} messages extracted.");
        source.AuditTrail.Add($"Extracted {messagesToMove.Count} messages to child ticket #{child.ConversationId} ({request.NewTitle}).");

        _conversations[child.ConversationId] = child;

        // Create link
        var link = new ConversationLinkRecord(
            Guid.NewGuid(),
            request.TenantId,
            source.ConversationId,
            child.ConversationId,
            HierarchyRelationshipType.SplitChild,
            true,
            now
        );
        _links[link.LinkId] = link;

        return Task.FromResult(new SplitConversationResult(source.ConversationId, child.ConversationId, messagesToMove.Count, now));
    }

    public Task<MergeConversationsResult> MergeConversationsAsync(MergeConversationsRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (request.TargetConversationId == Guid.Empty) throw new ArgumentException("TargetConversationId is required.", nameof(request));
        if (request.SourceConversationIds == null || request.SourceConversationIds.Count == 0)
        {
            throw new ArgumentException("At least one source conversation must be provided to merge.", nameof(request));
        }

        if (request.SourceConversationIds.Contains(request.TargetConversationId))
        {
            throw new InvalidOperationException("Cannot merge a conversation into itself.");
        }

        if (!_conversations.TryGetValue(request.TargetConversationId, out var target) || target.TenantId != request.TenantId)
        {
            throw new KeyNotFoundException($"Target conversation {request.TargetConversationId} not found.");
        }

        var now = DateTime.UtcNow;
        int reconciledCount = 0;
        var mergedIds = new List<Guid>();

        foreach (var srcId in request.SourceConversationIds)
        {
            if (_conversations.TryGetValue(srcId, out var src) && src.TenantId == request.TenantId)
            {
                // Reconcile messages
                foreach (var msg in src.Messages.Where(m => !m.IsSplitMoved))
                {
                    target.Messages.Add(new HierarchyMessageRecord
                    {
                        MessageId = Guid.NewGuid(),
                        ConversationId = target.ConversationId,
                        Sender = msg.Sender,
                        Text = $"[Merged from #{srcId}] {msg.Text}",
                        SentAtUtc = msg.SentAtUtc,
                        IsSplitMoved = false
                    });
                    reconciledCount++;
                }

                if (request.CloseSourceConversations)
                {
                    src.Status = HierarchyConversationStatus.Merged;
                    src.MergedIntoConversationId = target.ConversationId;
                    src.AuditTrail.Add($"Merged into target conversation #{target.ConversationId} at {now:O}. Reason: {request.Reason}.");
                }

                target.AuditTrail.Add($"Merged ticket #{srcId} ({src.Title}). Reconciled {src.Messages.Count} messages.");

                var link = new ConversationLinkRecord(
                    Guid.NewGuid(),
                    request.TenantId,
                    target.ConversationId,
                    srcId,
                    HierarchyRelationshipType.MergedSource,
                    false,
                    now
                );
                _links[link.LinkId] = link;
                mergedIds.Add(srcId);
            }
        }

        // Re-sort target messages chronologically
        target.Messages.Sort((a, b) => a.SentAtUtc.CompareTo(b.SentAtUtc));

        return Task.FromResult(new MergeConversationsResult(target.ConversationId, mergedIds, reconciledCount, now));
    }

    public Task<ConversationLinkRecord> LinkConversationsAsync(Guid tenantId, Guid parentId, Guid childId, HierarchyRelationshipType relationship, bool isBlocking = false, CancellationToken ct = default)
    {
        if (parentId == childId)
        {
            throw new InvalidOperationException("Cannot link a conversation to itself.");
        }

        // Check for circular dependency
        if (IsAncestor(tenantId, childId, parentId))
        {
            throw new InvalidOperationException("Circular link detected: cannot make an ancestor conversation a child.");
        }

        var link = new ConversationLinkRecord(
            Guid.NewGuid(),
            tenantId,
            parentId,
            childId,
            relationship,
            isBlocking,
            DateTime.UtcNow
        );

        _links[link.LinkId] = link;
        return Task.FromResult(link);
    }

    public Task<bool> UnlinkConversationsAsync(Guid tenantId, Guid parentId, Guid childId, CancellationToken ct = default)
    {
        var matching = _links.Values.FirstOrDefault(l =>
            l.TenantId == tenantId &&
            l.ParentConversationId == parentId &&
            l.ChildConversationId == childId);

        if (matching != null && _links.TryRemove(matching.LinkId, out _))
        {
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<ConversationHierarchyNode> GetHierarchyTreeAsync(Guid tenantId, Guid rootConversationId, CancellationToken ct = default)
    {
        _conversations.TryGetValue(rootConversationId, out var root);
        var title = root?.Title ?? "Unknown Ticket";
        var status = root?.Status ?? HierarchyConversationStatus.Open;

        var visited = new HashSet<Guid> { rootConversationId };
        var rootNode = BuildHierarchyNode(tenantId, rootConversationId, title, status, HierarchyRelationshipType.ParentChild, false, visited);

        return Task.FromResult(rootNode);
    }

    private ConversationHierarchyNode BuildHierarchyNode(Guid tenantId, Guid convId, string title, HierarchyConversationStatus status, HierarchyRelationshipType rel, bool isBlocking, HashSet<Guid> visited)
    {
        var childLinks = _links.Values
            .Where(l => l.TenantId == tenantId && l.ParentConversationId == convId)
            .ToList();

        var children = new List<ConversationHierarchyNode>();

        foreach (var cl in childLinks)
        {
            if (visited.Contains(cl.ChildConversationId)) continue;
            visited.Add(cl.ChildConversationId);

            _conversations.TryGetValue(cl.ChildConversationId, out var childConv);
            var cTitle = childConv?.Title ?? "Child Ticket";
            var cStatus = childConv?.Status ?? HierarchyConversationStatus.Open;

            var childNode = BuildHierarchyNode(tenantId, cl.ChildConversationId, cTitle, cStatus, cl.Relationship, cl.IsBlocking, visited);
            children.Add(childNode);
        }

        return new ConversationHierarchyNode(convId, title, status, rel, isBlocking, children);
    }

    public Task<HierarchyTreeSummary> CanCloseConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        var blockingReasons = new List<string>();
        int totalDescendants = 0;
        int openCount = 0;
        int resolvedCount = 0;

        var visited = new HashSet<Guid> { conversationId };
        var queue = new Queue<Guid>();
        queue.Enqueue(conversationId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var links = _links.Values.Where(l => l.TenantId == tenantId && l.ParentConversationId == current).ToList();

            foreach (var l in links)
            {
                if (visited.Add(l.ChildConversationId))
                {
                    totalDescendants++;
                    queue.Enqueue(l.ChildConversationId);

                    if (_conversations.TryGetValue(l.ChildConversationId, out var child))
                    {
                        if (child.Status == HierarchyConversationStatus.Resolved ||
                            child.Status == HierarchyConversationStatus.Closed ||
                            child.Status == HierarchyConversationStatus.Merged)
                        {
                            resolvedCount++;
                        }
                        else
                        {
                            openCount++;
                            if (l.IsBlocking)
                            {
                                blockingReasons.Add($"Child ticket #{child.ConversationId} ('{child.Title}') is still {child.Status}.");
                            }
                        }
                    }
                }
            }
        }

        bool canClose = blockingReasons.Count == 0;

        return Task.FromResult(new HierarchyTreeSummary(
            conversationId,
            totalDescendants,
            openCount,
            resolvedCount,
            canClose,
            blockingReasons
        ));
    }

    private bool IsAncestor(Guid tenantId, Guid candidateAncestorId, Guid descendantId)
    {
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(descendantId);

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();

            var parents = _links.Values
                .Where(l => l.TenantId == tenantId && l.ChildConversationId == curr)
                .Select(l => l.ParentConversationId);

            foreach (var p in parents)
            {
                if (p == candidateAncestorId) return true;
                if (visited.Add(p)) queue.Enqueue(p);
            }
        }

        return false;
    }
}
