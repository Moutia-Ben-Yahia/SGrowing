using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Xunit;

namespace SGrowing.Tests.Outbound;

public class PowerDialerCadenceTests
{
    private readonly Mock<IOutbox> _outboxMock = new();

    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task GetDialerQueueAsync_WhenPendingVoiceTouchpointsExist_ReturnsMappedQueueItems()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new PowerDialerService(db, _outboxMock.Object);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(
            tenantId,
            "Tarek",
            "Ben Salem",
            "tarek@tunis-supply.tn",
            "+21698112233"
        );
        db.Contacts.Add(contact);

        var voiceExec = new TouchpointExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contact.Id,
            TouchpointId = Guid.NewGuid(),
            Channel = TouchpointChannel.VoiceCall,
            ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-5),
            Status = "Pending"
        };
        db.Set<TouchpointExecution>().Add(voiceExec);
        await db.SaveChangesAsync();

        // Act
        var queue = await service.GetDialerQueueAsync(tenantId);

        // Assert
        queue.Should().NotBeEmpty();
        var item = queue.First(q => q.ContactId == contact.Id);
        item.FullName.Should().Be("Tarek Ben Salem");
        item.PhoneNumber.Should().Be("+21698112233");
        item.Email.Should().Be("tarek@tunis-supply.tn");
    }

    [Fact]
    public async Task GetDialerQueueAsync_WhenNoExecutions_ReturnsFallbackContactsFromDb()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new PowerDialerService(db, _outboxMock.Object);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(
            tenantId,
            "Youssef",
            "Trabelsi",
            "youssef@med-freight.tn",
            "+21622334455"
        );
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        // Act
        var queue = await service.GetDialerQueueAsync(tenantId);

        // Assert
        queue.Should().NotBeEmpty();
        queue.Should().Contain(q => q.ContactId == contact.Id && q.FullName == "Youssef Trabelsi");
    }

    [Fact]
    public async Task LogCallOutcomeAsync_WithMeetingBooked_CreatesActivityAndCompletesTouchpoint()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new PowerDialerService(db, _outboxMock.Object);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(
            tenantId,
            "Leila",
            "Mansouri",
            "leila@pharmadistrib.tn",
            "+21671998877"
        );
        db.Contacts.Add(contact);

        var voiceExec = new TouchpointExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contact.Id,
            TouchpointId = Guid.NewGuid(),
            Channel = TouchpointChannel.VoiceCall,
            ScheduledAtUtc = DateTime.UtcNow.AddHours(-1),
            Status = "Pending"
        };
        db.Set<TouchpointExecution>().Add(voiceExec);
        await db.SaveChangesAsync();

        // Act
        var result = await service.LogCallOutcomeAsync(
            tenantId,
            contact.Id,
            "MeetingBooked",
            durationSeconds: 195,
            notes: "Prospect requested demo on Monday at 10 AM. Very interested."
        );

        // Assert
        result.Should().NotBeNull();
        result.ContactId.Should().Be(contact.Id);
        result.Outcome.Should().Be("MeetingBooked");
        result.DurationSeconds.Should().Be(195);
        result.NextActionDescription.Should().Contain("Meeting booked!");

        // 1. Verify Activity was persisted
        var activity = await db.Activities.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.ContactId == contact.Id);
        activity.Should().NotBeNull();
        activity!.Type.Should().Be(ActivityType.Call);
        activity.PayloadJson.Should().Contain("MeetingBooked");
        activity.PayloadJson.Should().Contain("Very interested");

        // 2. Verify TouchpointExecution was completed
        var updatedExec = await db.Set<TouchpointExecution>().FirstAsync(e => e.Id == voiceExec.Id);
        updatedExec.Status.Should().Be("Completed");
        updatedExec.ExecutedAtUtc.Should().NotBeNull();

        // 3. Verify Outbox event was enqueued
        _outboxMock.Verify(o => o.EnqueueAsync(
            tenantId,
            "Outbound.CallOutcomeLogged",
            It.IsAny<object>(),
            default),
            Times.Once);
    }

    [Theory]
    [InlineData("LeftVoicemail", "Voicemail left. Automated follow-up email scheduled")]
    [InlineData("GatekeeperRefused", "Gatekeeper roadblock. Switch to LinkedIn direct outreach.")]
    [InlineData("NotInterested", "Contact marked not interested. Cadence stopped.")]
    public async Task LogCallOutcomeAsync_SetsAccurateNextActionDescription(string outcome, string expectedNextActionSubstring)
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new PowerDialerService(db, _outboxMock.Object);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Aymen", "Khemir", "aymen@trade.tn", "+21655443322");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        // Act
        var result = await service.LogCallOutcomeAsync(tenantId, contact.Id, outcome, 45, "Call details");

        // Assert
        result.NextActionDescription.Should().Contain(expectedNextActionSubstring);
    }

    [Fact]
    public async Task LogCallOutcomeAsync_WithInvalidContact_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new PowerDialerService(db, _outboxMock.Object);
        var tenantId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await service.LogCallOutcomeAsync(tenantId, Guid.NewGuid(), "LeftVoicemail", 10);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
