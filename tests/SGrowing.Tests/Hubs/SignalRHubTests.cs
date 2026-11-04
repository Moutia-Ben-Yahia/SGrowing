using FluentAssertions;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Core.Entities;
using Moq;
using Xunit;

namespace SGrowing.Tests.Hubs;

public class SignalRHubTests
{
    [Fact]
    public async Task IOmniClient_Contract_ShouldBeDefinedProperly()
    {
        var mockClient = new Mock<IOmniClient>();
        var message = new MessageDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MessageSenderType.Contact,
            null,
            MessageContentType.Text,
            "Realtime message test",
            null,
            DeliveryStatus.Delivered,
            DateTime.UtcNow
        );

        mockClient.Setup(c => c.OnMessageReceived(message)).Returns(Task.CompletedTask);

        var act = async () => await mockClient.Object.OnMessageReceived(message);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IOmniClient_OnVoiceTranscriptionChunk_ShouldBeDefinedProperly()
    {
        var mockClient = new Mock<IOmniClient>();
        var sessionId = Guid.NewGuid();

        mockClient.Setup(c => c.OnVoiceTranscriptionChunk(
            sessionId, "Caller", "What are your enterprise prices?", 0.75, true))
            .Returns(Task.CompletedTask);

        var act = async () => await mockClient.Object.OnVoiceTranscriptionChunk(
            sessionId, "Caller", "What are your enterprise prices?", 0.75, true);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IOmniClient_OnTypingIndicator_ShouldBeCallable()
    {
        var mockClient = new Mock<IOmniClient>();
        var conversationId = Guid.NewGuid();

        mockClient.Setup(c => c.OnTypingIndicator(conversationId, "Agent Smith", true))
            .Returns(Task.CompletedTask);

        var act = async () => await mockClient.Object.OnTypingIndicator(conversationId, "Agent Smith", true);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IOmniClient_OnConversationLockedAndUnlocked_ShouldBeCallable()
    {
        var mockClient = new Mock<IOmniClient>();
        var conversationId = Guid.NewGuid();

        mockClient.Setup(c => c.OnConversationLocked(conversationId, "Agent Cooper")).Returns(Task.CompletedTask);
        mockClient.Setup(c => c.OnConversationUnlocked(conversationId)).Returns(Task.CompletedTask);

        var actLock = async () => await mockClient.Object.OnConversationLocked(conversationId, "Agent Cooper");
        var actUnlock = async () => await mockClient.Object.OnConversationUnlocked(conversationId);

        await actLock.Should().NotThrowAsync();
        await actUnlock.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IOmniClient_OnOperatorPresenceChanged_ShouldBeCallable()
    {
        var mockClient = new Mock<IOmniClient>();
        var convId = Guid.NewGuid();

        mockClient.Setup(c => c.OnOperatorPresenceChanged(convId, "op_123", "Operator Trinity", false))
            .Returns(Task.CompletedTask);

        var act = async () => await mockClient.Object.OnOperatorPresenceChanged(convId, "op_123", "Operator Trinity", false);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IOmniClient_OnSlaBreachAlert_ShouldBeCallable()
    {
        var mockClient = new Mock<IOmniClient>();
        var convId = Guid.NewGuid();

        mockClient.Setup(c => c.OnSlaBreachAlert(convId, "Urgent", 5, false))
            .Returns(Task.CompletedTask);

        var act = async () => await mockClient.Object.OnSlaBreachAlert(convId, "Urgent", 5, false);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IOmniClient_OnOrderLockedAndUnlocked_ShouldBeCallable()
    {
        var mockClient = new Mock<IOmniClient>();
        var orderId = Guid.NewGuid();
        var expires = DateTime.UtcNow.AddMinutes(10);

        mockClient.Setup(c => c.OnOrderLocked(orderId, "agt_1", "Sarah", expires)).Returns(Task.CompletedTask);
        mockClient.Setup(c => c.OnOrderUnlocked(orderId)).Returns(Task.CompletedTask);

        var actLock = async () => await mockClient.Object.OnOrderLocked(orderId, "agt_1", "Sarah", expires);
        var actUnlock = async () => await mockClient.Object.OnOrderUnlocked(orderId);

        await actLock.Should().NotThrowAsync();
        await actUnlock.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IOmniClient_OnAiDraftGenerated_ShouldBeCallable()
    {
        var mockClient = new Mock<IOmniClient>();
        var draft = new AiDraftDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Thank you for contacting support. How may we assist you?",
            0.92f,
            "Ready",
            DateTime.UtcNow
        );

        mockClient.Setup(c => c.OnAiDraftGenerated(draft)).Returns(Task.CompletedTask);

        var act = async () => await mockClient.Object.OnAiDraftGenerated(draft);
        await act.Should().NotThrowAsync();
    }
}
