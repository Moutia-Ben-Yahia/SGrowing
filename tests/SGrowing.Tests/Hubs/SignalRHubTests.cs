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
}
