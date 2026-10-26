using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Calendar;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Modules.SEO;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterGrowthAndAcquisitionE2ETests
{
    private readonly Mock<IOutbox> _outboxMock = new();

    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MasterGrowthAcquisitionDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Master_Growth_And_Acquisition_FullFlywheel_Should_Succeed()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        // Register Tenant
        var tenant = new Tenant { Id = tenantId, Name = "SGrowing Enterprise", Slug = "sgrowing-ent" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // =========================================================================
        // ENGINE 1: Technical SEO & GEO Brand Citability Radar
        // =========================================================================
        var seoService = new SeoAuditService(db);
        var auditInput = new List<PageAuditInputDto>
        {
            new(
                "https://sgrowing-ent.io/",
                200,
                "SGrowing - B2B Autonomous Growth Engine",
                "Unified omnichannel marketing, SDR station, and calendar scheduling.",
                "Transform Your Inbound & Outbound Pipeline",
                HasCanonical: true,
                HasOpenGraph: true,
                HasSchemaOrg: true,
                LoadTimeMs: 180
            ),
            new(
                "https://sgrowing-ent.io/pricing",
                200,
                "Enterprise Transparent Pricing - SGrowing",
                "Pay per booked meeting and active channel nodes.",
                "Predictable Scalable Pricing",
                HasCanonical: true,
                HasOpenGraph: true,
                HasSchemaOrg: true,
                LoadTimeMs: 210
            )
        };

        var auditReport = await seoService.RunAuditAsync(tenantId, "https://sgrowing-ent.io", auditInput);
        auditReport.Should().NotBeNull();
        auditReport.OverallHealthScore.Should().Be(100);
        auditReport.ScannedPagesCount.Should().Be(2);

        // Record AI Engine Citations
        await seoService.RecordAeoMentionAsync(tenantId, "SGrowing", "ChatGPT", "Top CRM with SDR softphone", true, "Positive", "SGrowing leads B2B automation", 1);
        await seoService.RecordAeoMentionAsync(tenantId, "SGrowing", "Claude", "Modern omnichannel scheduler", true, "Positive", "SGrowing offers self-hosted calendar", 1);

        var geoScore = await seoService.ComputeGeoBrandCitabilityScoreAsync(tenantId, "SGrowing", "https://sgrowing-ent.io");
        geoScore.CitabilityRatePercent.Should().Be(100);
        geoScore.OverallScore.Should().BeGreaterThan(80);
        geoScore.AiEnginesEvaluated.Should().HaveCount(4);

        // =========================================================================
        // ENGINE 2: Social Media Multi-Channel Broadcast & AI Deflector
        // =========================================================================
        var socialService = new SocialPublishingService(_outboxMock.Object);
        var broadcastPost = await socialService.CreatePostAsync(
            tenantId,
            "Autonomous B2B pipelines: Combine SEO, Social, AI SDR, and Calendar into one unified engine.",
            new List<SocialPlatform> { SocialPlatform.X, SocialPlatform.LinkedIn, SocialPlatform.Telegram },
            scheduledAtUtc: DateTime.UtcNow.AddMinutes(-1), // Due immediately
            mediaUrls: new List<string> { "https://cdn.sgrowing.io/infographic.png" }
        );

        var publishedList = await socialService.PublishDuePostsAsync(tenantId);
        publishedList.Should().ContainSingle(p => p.Id == broadcastPost.Id);
        publishedList[0].Status.Should().Be(SocialPostStatus.Published);

        // Public comment deflection to DM
        var deflectionService = new SocialCommentDeflectionService(_outboxMock.Object);
        var deflectionResult = await deflectionService.HandlePublicCommentAsync(
            tenantId,
            SocialPlatform.LinkedIn,
            "post_broadcast_1",
            "alex_director",
            "This looks promising! Where can I see pricing and book a live demo?"
        );
        deflectionResult.Deflected.Should().BeTrue();
        deflectionResult.ReplyText.Should().Contain("DM with details");

        // =========================================================================
        // ENGINE 3: Autonomous AI SDR & Power Calling Station
        // =========================================================================
        var leadContact = Contact.Create(
            tenantId,
            "Alex",
            "Director",
            "alex.director@enterprisegrowth.com",
            "+21698445566",
            LifecycleStage.Lead
        );
        db.Contacts.Add(leadContact);

        var touchpointExec = new TouchpointExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = leadContact.Id,
            TouchpointId = Guid.NewGuid(),
            Channel = TouchpointChannel.VoiceCall,
            ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-10),
            Status = "Pending"
        };
        db.Set<TouchpointExecution>().Add(touchpointExec);
        await db.SaveChangesAsync();

        var powerDialer = new PowerDialerService(db, _outboxMock.Object);
        var dialerQueue = await powerDialer.GetDialerQueueAsync(tenantId);
        dialerQueue.Should().Contain(q => q.ContactId == leadContact.Id);

        // Execute Call & Log Outcome: MeetingBooked
        var callOutcome = await powerDialer.LogCallOutcomeAsync(
            tenantId,
            leadContact.Id,
            "MeetingBooked",
            durationSeconds: 240,
            notes: "Prospect requested a 30-min architecture consultation for Friday at 10 AM UTC."
        );
        callOutcome.Outcome.Should().Be("MeetingBooked");
        callOutcome.NextActionDescription.Should().Contain("Meeting booked!");

        var refreshedTouchpoint = await db.Set<TouchpointExecution>().FirstAsync(e => e.Id == touchpointExec.Id);
        refreshedTouchpoint.Status.Should().Be("Completed");

        // =========================================================================
        // ENGINE 4: Calendar & Public Appointment Scheduling Engine
        // =========================================================================
        var calendarService = new CalendarSchedulingService(db);
        var hostUserId = Guid.NewGuid();

        var demoEventType = await calendarService.CreateEventTypeAsync(
            tenantId,
            "Enterprise Architecture Consultation",
            "architecture-demo",
            "30 min 1-on-1 architecture review",
            30,
            isRoundRobin: false,
            hosts: new List<Guid> { hostUserId }
        );
        demoEventType.Slug.Should().Be("architecture-demo");

        // Fetch Public Booking Page
        var meetingDateUtc = DateTime.UtcNow.Date.AddDays(2).AddHours(10); // 10:00 UTC
        var publicPage = await calendarService.GetPublicBookingPageAsync("sgrowing-ent", "architecture-demo", meetingDateUtc);
        publicPage.Should().NotBeNull();
        publicPage!.Value.Slots.Should().NotBeEmpty();

        // Book Appointment
        var booking = await calendarService.BookPublicAppointmentAsync(
            "sgrowing-ent",
            "architecture-demo",
            "Alex Director",
            "alex.director@enterprisegrowth.com",
            "+21698445566",
            meetingDateUtc,
            "UTC",
            "Integration review"
        );
        booking.Should().NotBeNull();
        booking.Status.Should().Be(CalendarBookingStatus.Scheduled);
        booking.MeetingUrl.Should().StartWith("https://meet.sgrowing.io/room/");

        // Verify Conflict Guard prevents double booking
        var conflictAct = async () => await calendarService.BookPublicAppointmentAsync(
            "sgrowing-ent",
            "architecture-demo",
            "Other Prospect",
            "other@prospect.com",
            "+12345678",
            meetingDateUtc,
            "UTC"
        );
        await conflictAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already booked*");

        // Generate .ics calendar invite
        var icsPayload = await calendarService.GenerateIcsContentAsync(tenantId, booking.Id);
        icsPayload.Should().Contain("BEGIN:VCALENDAR");
        icsPayload.Should().Contain("BEGIN:VEVENT");
        icsPayload.Should().Contain("SUMMARY:Enterprise Architecture Consultation");
        icsPayload.Should().Contain("alex.director@enterprisegrowth.com");
        icsPayload.Should().Contain("END:VCALENDAR");

        // =========================================================================
        // ENGINE 5: Customer Journey Automation Verification
        // =========================================================================
        var journey = CustomerJourney.Create(
            tenantId,
            "Post-Booking Onboarding Flow",
            "Appointment.Booked"
        );
        db.CustomerJourneys.Add(journey);
        await db.SaveChangesAsync();

        var storedJourney = await db.CustomerJourneys.FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == journey.Id);
        storedJourney.Should().NotBeNull();
        storedJourney!.Name.Should().Be("Post-Booking Onboarding Flow");
        storedJourney.TriggerEvent.Should().Be("Appointment.Booked");
        storedJourney.Status.Should().Be(JourneyStatus.Active);

        // Pause / Draft journey
        storedJourney.Status = JourneyStatus.Paused;
        await db.SaveChangesAsync();

        var pausedJourney = await db.CustomerJourneys.FirstAsync(j => j.Id == journey.Id);
        pausedJourney.Status.Should().Be(JourneyStatus.Paused);
    }
}
