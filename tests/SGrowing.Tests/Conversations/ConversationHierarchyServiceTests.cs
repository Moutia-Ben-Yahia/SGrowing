using FluentAssertions;
using SGrowing.Api.Modules.Conversations.Hierarchy;
using Xunit;

namespace SGrowing.Tests.Conversations;

public class ConversationHierarchyServiceTests
{
    private readonly ConversationHierarchyService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    private async Task<HierarchyConversationRecord> CreateSampleConversationAsync(string title, int messageCount = 3)
    {
        var conv = new HierarchyConversationRecord
        {
            TenantId = _tenantId,
            Title = title,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Amira Ben Sassi",
            Channel = "WhatsApp",
            Status = HierarchyConversationStatus.Open
        };

        for (int i = 1; i <= messageCount; i++)
        {
            conv.Messages.Add(new HierarchyMessageRecord
            {
                ConversationId = conv.ConversationId,
                Sender = i % 2 == 1 ? "Customer" : "Agent",
                Text = $"Message {i} regarding order and invoice",
                SentAtUtc = DateTime.UtcNow.AddMinutes(-30 + i * 5)
            });
        }

        return await _service.RegisterConversationAsync(conv);
    }

    [Fact]
    public async Task SplitConversationAsync_ShouldExtractMessages_AndCreateChildConversation()
    {
        var source = await CreateSampleConversationAsync("Order Damaged + Invoice Question", messageCount: 4);
        var msgToMove = source.Messages[2]; // 3rd message

        var splitReq = new SplitConversationRequest(
            _tenantId,
            source.ConversationId,
            new List<Guid> { msgToMove.MessageId },
            "Invoice Billing Query (Split from #Order)",
            Reason: SplitReason.MultipleInquiries
        );

        var result = await _service.SplitConversationAsync(splitReq);

        result.Should().NotBeNull();
        result.SourceConversationId.Should().Be(source.ConversationId);
        result.NewChildConversationId.Should().NotBeEmpty();
        result.MovedMessagesCount.Should().Be(1);

        // Verify child
        var child = await _service.GetConversationAsync(_tenantId, result.NewChildConversationId);
        child.Should().NotBeNull();
        child!.ParentId.Should().Be(source.ConversationId);
        child.SplitFromConversationId.Should().Be(source.ConversationId);
        child.Messages.Should().ContainSingle(m => m.Text == msgToMove.Text);
        child.CustomerName.Should().Be(source.CustomerName);

        // Verify source updated
        msgToMove.IsSplitMoved.Should().BeTrue();
        source.AuditTrail.Should().Contain(a => a.Contains("Extracted 1 messages"));
    }

    [Fact]
    public async Task SplitConversationAsync_ShouldThrow_WhenSourceNotFound()
    {
        var splitReq = new SplitConversationRequest(_tenantId, Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }, "Split");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.SplitConversationAsync(splitReq));
    }

    [Fact]
    public async Task SplitConversationAsync_ShouldThrow_WhenMessagesDoNotExistInSource()
    {
        var source = await CreateSampleConversationAsync("Ticket 101", 1);
        var invalidMsgId = Guid.NewGuid();

        var splitReq = new SplitConversationRequest(_tenantId, source.ConversationId, new List<Guid> { invalidMsgId }, "Split");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SplitConversationAsync(splitReq));
    }

    [Fact]
    public async Task MergeConversationsAsync_ShouldReconcileMessages_AndCloseSourceTickets()
    {
        var target = await CreateSampleConversationAsync("Master Ticket: Shipment Delay", 2);
        var source1 = await CreateSampleConversationAsync("Duplicate: Where is my order?", 2);
        var source2 = await CreateSampleConversationAsync("Duplicate WhatsApp: Order status", 1);

        var mergeReq = new MergeConversationsRequest(
            _tenantId,
            target.ConversationId,
            new List<Guid> { source1.ConversationId, source2.ConversationId },
            MergeReason.DuplicateTicket,
            "Customer submitted duplicate requests across channels."
        );

        var result = await _service.MergeConversationsAsync(mergeReq);

        result.TargetConversationId.Should().Be(target.ConversationId);
        result.MergedSourceIds.Should().HaveCount(2);
        result.ReconciledMessagesCount.Should().Be(3);

        // Target should now have 2 initial + 3 reconciled = 5 messages
        target.Messages.Should().HaveCount(5);

        // Sources should be marked Merged with redirection pointer
        source1.Status.Should().Be(HierarchyConversationStatus.Merged);
        source1.MergedIntoConversationId.Should().Be(target.ConversationId);

        source2.Status.Should().Be(HierarchyConversationStatus.Merged);
        source2.MergedIntoConversationId.Should().Be(target.ConversationId);
    }

    [Fact]
    public async Task MergeConversationsAsync_ShouldThrow_WhenSelfMergeAttempted()
    {
        var conv = await CreateSampleConversationAsync("Ticket Self", 1);

        var mergeReq = new MergeConversationsRequest(
            _tenantId,
            conv.ConversationId,
            new List<Guid> { conv.ConversationId },
            MergeReason.DuplicateTicket
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.MergeConversationsAsync(mergeReq));
    }

    [Fact]
    public async Task LinkConversationsAsync_ShouldDetectAndPreventCircularLinks()
    {
        var parent = await CreateSampleConversationAsync("Parent Epic", 1);
        var child = await CreateSampleConversationAsync("Sub Ticket A", 1);
        var grandChild = await CreateSampleConversationAsync("Sub Ticket A.1", 1);

        await _service.LinkConversationsAsync(_tenantId, parent.ConversationId, child.ConversationId, HierarchyRelationshipType.ParentChild);
        await _service.LinkConversationsAsync(_tenantId, child.ConversationId, grandChild.ConversationId, HierarchyRelationshipType.ParentChild);

        // Linking parent as a child of grandchild would create a circular dependency
        var act = () => _service.LinkConversationsAsync(_tenantId, grandChild.ConversationId, parent.ConversationId, HierarchyRelationshipType.ParentChild);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CanCloseConversationAsync_ShouldBlockParentClosure_UntilChildIsResolved()
    {
        var parent = await CreateSampleConversationAsync("Master Hardware Repair", 1);
        var child1 = await CreateSampleConversationAsync("Order Spare Parts", 1);
        var child2 = await CreateSampleConversationAsync("Technician Schedule", 1);

        await _service.LinkConversationsAsync(_tenantId, parent.ConversationId, child1.ConversationId, HierarchyRelationshipType.ParentChild, isBlocking: true);
        await _service.LinkConversationsAsync(_tenantId, parent.ConversationId, child2.ConversationId, HierarchyRelationshipType.ParentChild, isBlocking: true);

        // Both children are open
        var check1 = await _service.CanCloseConversationAsync(_tenantId, parent.ConversationId);
        check1.CanParentClose.Should().BeFalse();
        check1.TotalDescendants.Should().Be(2);
        check1.OpenCount.Should().Be(2);
        check1.BlockingReasons.Should().HaveCount(2);

        // Resolve first child
        child1.Status = HierarchyConversationStatus.Resolved;
        var check2 = await _service.CanCloseConversationAsync(_tenantId, parent.ConversationId);
        check2.CanParentClose.Should().BeFalse();
        check2.OpenCount.Should().Be(1);
        check2.ResolvedCount.Should().Be(1);

        // Resolve second child
        child2.Status = HierarchyConversationStatus.Resolved;
        var check3 = await _service.CanCloseConversationAsync(_tenantId, parent.ConversationId);
        check3.CanParentClose.Should().BeTrue();
        check3.BlockingReasons.Should().BeEmpty();
        check3.ResolvedCount.Should().Be(2);
    }

    [Fact]
    public async Task GetHierarchyTreeAsync_ShouldReturnHierarchicalGraph()
    {
        var root = await CreateSampleConversationAsync("Root Ticket", 1);
        var child = await CreateSampleConversationAsync("Child Ticket", 1);

        await _service.LinkConversationsAsync(_tenantId, root.ConversationId, child.ConversationId, HierarchyRelationshipType.ParentChild);

        var tree = await _service.GetHierarchyTreeAsync(_tenantId, root.ConversationId);

        tree.Should().NotBeNull();
        tree.ConversationId.Should().Be(root.ConversationId);
        tree.Children.Should().ContainSingle(c => c.ConversationId == child.ConversationId);
    }
}
