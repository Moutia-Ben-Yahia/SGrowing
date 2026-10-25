using FluentAssertions;
using MarketingStudio.Api.Hubs;
using MarketingStudio.Api.Modules.Conversations;
using MarketingStudio.Core.Entities;
using Moq;
using Xunit;

namespace MarketingStudio.Tests.Hubs;

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
}
