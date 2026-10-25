using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGrowing.Tests.Voice;

public class VoiceTelephonyTests
{
    private VoiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new VoiceDbContext(options, tenantContext);
    }

    [Fact]
    public async Task HandleInboundCallWebhookAsync_CreatesSessionAndReturnsTwiML()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new VoiceTelephonyService(db);
        var tenantId = Guid.NewGuid();
        var callSid = "CA_inbound_test_12345";
        var fromNumber = "+15551234567";
        var toNumber = "+15557654321";

        // Act
        var twiml = await service.HandleInboundCallWebhookAsync(tenantId, callSid, fromNumber, toNumber);

        // Assert TwiML
        twiml.Should().NotBeNullOrEmpty();
        twiml.Should().Contain("<Response>");
        twiml.Should().Contain("""<Say voice="alice">Hello! Thank you for calling SGrowing.</Say>""");
        twiml.Should().Contain("""<Gather input="speech" """);
        twiml.Should().Contain("</Response>");

        // Assert Session persisted in DB
        var session = await db.VoiceCallSessions.FirstOrDefaultAsync(s => s.CallSid == callSid && s.TenantId == tenantId);
        session.Should().NotBeNull();
        session!.Direction.Should().Be(CallDirection.Inbound);
        session.FromNumber.Should().Be(fromNumber);
        session.ToNumber.Should().Be(toNumber);
        session.Status.Should().Be(CallStatus.InProgress);

        // Assert Contact was resolved / created for caller
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == session.ContactId);
        contact.Should().NotBeNull();
        contact!.Phone.Should().Be(fromNumber);
    }

    [Fact]
    public async Task InitiateOutboundCallAsync_PersistsSessionWithOutboundDirectionAndRinging()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new VoiceTelephonyService(db);
        var tenantId = Guid.NewGuid();
        var contact = Contact.Create(tenantId, "Bruce", "Wayne", phone: "+15559998888");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var fromNumber = "+15550001111";
        var toNumber = contact.Phone!;

        // Act
        var session = await service.InitiateOutboundCallAsync(tenantId, contact.Id, fromNumber, toNumber);

        // Assert
        session.Should().NotBeNull();
        session.Id.Should().NotBeEmpty();
        session.TenantId.Should().Be(tenantId);
        session.ContactId.Should().Be(contact.Id);
        session.CallSid.Should().StartWith("CA");
        session.Direction.Should().Be(CallDirection.Outbound);
        session.Status.Should().Be(CallStatus.Ringing);
        session.FromNumber.Should().Be(fromNumber);
        session.ToNumber.Should().Be(toNumber);

        var persisted = await db.VoiceCallSessions.FindAsync(session.Id);
        persisted.Should().NotBeNull();
        persisted!.CallSid.Should().Be(session.CallSid);
    }

    [Fact]
    public async Task HandleCallStatusCallbackAsync_Completed_UpdatesStatusAndDuration()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new VoiceTelephonyService(db);
        var tenantId = Guid.NewGuid();
        var callSid = "CA_status_callback_999";
        var session = VoiceCallSession.Create(tenantId, Guid.NewGuid(), callSid, CallDirection.Inbound, "+111", "+222");
        db.VoiceCallSessions.Add(session);
        await db.SaveChangesAsync();

        // Act
        var updated = await service.HandleCallStatusCallbackAsync(tenantId, callSid, "completed", 125);

        // Assert
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(CallStatus.Completed);
        updated.DurationSeconds.Should().Be(125);

        var persisted = await db.VoiceCallSessions.FirstOrDefaultAsync(s => s.CallSid == callSid);
        persisted!.Status.Should().Be(CallStatus.Completed);
        persisted.DurationSeconds.Should().Be(125);
    }

    [Fact]
    public async Task HandleCallStatusCallbackAsync_Failed_SetsFailedStatus()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new VoiceTelephonyService(db);
        var tenantId = Guid.NewGuid();
        var callSid = "CA_failed_call_111";
        var session = VoiceCallSession.Create(tenantId, Guid.NewGuid(), callSid, CallDirection.Outbound, "+111", "+222");
        db.VoiceCallSessions.Add(session);
        await db.SaveChangesAsync();

        // Act
        var updated = await service.HandleCallStatusCallbackAsync(tenantId, callSid, "failed", 0);

        // Assert
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(CallStatus.Failed);
    }

    [Fact]
    public async Task RealtimeVoiceAgent_ProcessVoiceTurnAsync_ReasoningLoopAndTranscriptAccumulation()
    {
        // Arrange
        var db = CreateDbContext();
        var agent = new RealtimeVoiceAgentService(db);
        var tenantId = Guid.NewGuid();
        var session = VoiceCallSession.Create(tenantId, Guid.NewGuid(), "CA_agent_turn_test", CallDirection.Inbound, "+15551112222", "+15553334444");
        db.VoiceCallSessions.Add(session);
        await db.SaveChangesAsync();

        // Turn 1: Caller inquires about pricing
        var callerTurn1 = "Hi, could you give me more details on your pricing tiers?";
        var response1 = await agent.ProcessVoiceTurnAsync(tenantId, session.Id, callerTurn1);

        // Assert Turn 1
        response1.Should().NotBeNullOrWhiteSpace();
        response1.ToLowerInvariant().Should().Contain("starter");
        response1.ToLowerInvariant().Should().Contain("dollars");

        var sessionAfterTurn1 = await db.VoiceCallSessions.FindAsync(session.Id);
        sessionAfterTurn1!.Transcript.Should().NotBeNull();
        sessionAfterTurn1.Transcript.Should().Contain("Caller: Hi, could you give me more details on your pricing tiers?");
        sessionAfterTurn1.Transcript.Should().Contain("Agent:");

        // Turn 2: Caller requests a demo booking
        var callerTurn2 = "Sounds great, can I schedule a demo for Thursday morning?";
        var response2 = await agent.ProcessVoiceTurnAsync(tenantId, session.Id, callerTurn2);

        // Assert Turn 2
        response2.Should().NotBeNullOrWhiteSpace();
        response2.ToLowerInvariant().Should().Contain("demo");

        var sessionAfterTurn2 = await db.VoiceCallSessions.FindAsync(session.Id);
        sessionAfterTurn2!.Transcript.Should().NotBeNull();
        // Verify accumulation: contains BOTH turn 1 and turn 2
        sessionAfterTurn2.Transcript.Should().Contain(callerTurn1);
        sessionAfterTurn2.Transcript.Should().Contain(callerTurn2);
        sessionAfterTurn2.Transcript.Should().Contain(response1);
        sessionAfterTurn2.Transcript.Should().Contain(response2);
    }

    [Fact]
    public void VoiceCallSession_CompleteAndFail_SetsProperState()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var session = VoiceCallSession.Create(tenantId, contactId, "CA_entity_test", CallDirection.Inbound, "+123", "+456");

        session.Status.Should().Be(CallStatus.Ringing);
        session.DurationSeconds.Should().Be(0);

        // Act CompleteCall
        session.CompleteCall(65, "https://storage.sgrowing.com/recordings/rec_1.wav", "Caller: Hello\nAgent: Hi");

        // Assert
        session.Status.Should().Be(CallStatus.Completed);
        session.DurationSeconds.Should().Be(65);
        session.RecordingUrl.Should().Be("https://storage.sgrowing.com/recordings/rec_1.wav");
        session.Transcript.Should().Be("Caller: Hello\nAgent: Hi");

        // Act FailCall
        session.FailCall();
        session.Status.Should().Be(CallStatus.Failed);
    }
}
