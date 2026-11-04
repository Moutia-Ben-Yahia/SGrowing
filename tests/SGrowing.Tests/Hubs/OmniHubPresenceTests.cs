using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SGrowing.Api.Hubs;
using Xunit;

namespace SGrowing.Tests.Hubs;

public class OmniHubPresenceTests
{
    private readonly Mock<IHubCallerClients<IOmniClient>> _mockClients;
    private readonly Mock<IOmniClient> _mockClient;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly OmniHub _hub;
    private const string ConnectionId = "conn_test_9921";

    public OmniHubPresenceTests()
    {
        _mockClients = new Mock<IHubCallerClients<IOmniClient>>();
        _mockClient = new Mock<IOmniClient>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        _mockContext.Setup(c => c.ConnectionId).Returns(ConnectionId);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClient.Object);

        _hub = new OmniHub
        {
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    [Fact]
    public async Task JoinOrder_ShouldAddConnectionToOrderGroup()
    {
        var orderId = Guid.NewGuid();
        var expectedGroup = $"order_{orderId}";

        await _hub.JoinOrder(orderId);

        _mockGroups.Verify(g => g.AddToGroupAsync(ConnectionId, expectedGroup, default), Times.Once);
    }

    [Fact]
    public async Task LeaveOrder_ShouldRemoveConnectionFromOrderGroup()
    {
        var orderId = Guid.NewGuid();
        var expectedGroup = $"order_{orderId}";

        await _hub.LeaveOrder(orderId);

        _mockGroups.Verify(g => g.RemoveFromGroupAsync(ConnectionId, expectedGroup, default), Times.Once);
    }

    [Fact]
    public async Task BroadcastOrderTyping_ShouldNotifyOrderGroupWithBeacon()
    {
        var orderId = Guid.NewGuid();
        var expectedGroup = $"order_{orderId}";
        const string opId = "op_alex_01";
        const string opName = "Alex Vance";
        const string draft = "Verifying shipment status with carrier...";

        await _hub.BroadcastOrderTyping(orderId, opId, opName, isTyping: true, draft);

        _mockClients.Verify(c => c.Group(expectedGroup), Times.Once);
        _mockClient.Verify(c => c.OnOrderTypingBeacon(orderId, opId, opName, true, draft), Times.Once);
    }

    [Fact]
    public async Task BroadcastOrderLock_ShouldNotifyTenantGroup()
    {
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddMinutes(5);
        var expectedGroup = $"tenant_{tenantId}";

        await _hub.BroadcastOrderLock(tenantId, orderId, "op_1", "Elena Rostova", expiresAt);

        _mockClients.Verify(c => c.Group(expectedGroup), Times.Once);
        _mockClient.Verify(c => c.OnOrderLocked(orderId, "op_1", "Elena Rostova", expiresAt), Times.Once);
    }

    [Fact]
    public async Task BroadcastOrderUnlock_ShouldNotifyTenantGroup()
    {
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var expectedGroup = $"tenant_{tenantId}";

        await _hub.BroadcastOrderUnlock(tenantId, orderId);

        _mockClients.Verify(c => c.Group(expectedGroup), Times.Once);
        _mockClient.Verify(c => c.OnOrderUnlocked(orderId), Times.Once);
    }

    [Fact]
    public void GroupNameHelpers_ShouldFormatDeterministicGroupNames()
    {
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");

        OmniHub.GetOrderGroup(guid).Should().Be($"order_{guid}");
        OmniHub.GetTenantGroup(guid).Should().Be($"tenant_{guid}");
        OmniHub.GetConversationGroup(guid).Should().Be($"conv_{guid}");
    }

    [Fact]
    public async Task JoinAndLeaveConversation_ShouldAddAndRemoveConnectionFromConversationGroup()
    {
        var conversationId = Guid.NewGuid();
        var expectedGroup = $"conv_{conversationId}";

        await _hub.JoinConversation(conversationId);
        _mockGroups.Verify(g => g.AddToGroupAsync(ConnectionId, expectedGroup, default), Times.Once);

        await _hub.LeaveConversation(conversationId);
        _mockGroups.Verify(g => g.RemoveFromGroupAsync(ConnectionId, expectedGroup, default), Times.Once);
    }

    [Fact]
    public async Task BroadcastTyping_ShouldNotifyConversationGroupWithSenderDetails()
    {
        var conversationId = Guid.NewGuid();
        var expectedGroup = $"conv_{conversationId}";
        const string senderName = "Sarah Agent";

        await _hub.BroadcastTyping(conversationId, senderName, isTyping: true);

        _mockClients.Verify(c => c.Group(expectedGroup), Times.Once);
        _mockClient.Verify(c => c.OnTypingIndicator(conversationId, senderName, true), Times.Once);
    }
}
