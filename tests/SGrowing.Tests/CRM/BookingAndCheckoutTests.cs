using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace SGrowing.Tests.CRM;

public class BookingAndCheckoutTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public async Task BookSlot_CreatesConfirmedBookingAndTagsContact()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new AppointmentBookingService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var slot = AppointmentSlot.Create(tenantId, "Executive Enterprise Demo", 45, "Sarah Chen");
        db.AppointmentSlots.Add(slot);
        var contact = Contact.Create(tenantId, "Steve", "Rogers", "steve@avengers.org");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var scheduledTime = DateTime.UtcNow.AddDays(2);
        var res = await service.BookSlotAsync(tenantId, slot.Id, contact.Id, scheduledTime);

        res.IsSuccess.Should().BeTrue();
        res.Value.Status.Should().Be(BookingStatus.Confirmed);
        res.Value.MeetingUrl.Should().Contain("https://meet.sgrowing.com");

        var updatedContact = await db.Contacts.FindAsync(contact.Id);
        updatedContact!.Tags.Should().Contain("demo-scheduled");
    }

    [Fact]
    public async Task CheckoutLink_WhenPaid_CreatesWonDealAndPromotesContactToCustomer()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new CheckoutLinkService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Tony", "Stark", "tony@stark.com");
        db.Contacts.Add(contact);
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var linkRes = await service.CreateCheckoutLinkAsync(tenantId, contact.Id, conv.Id, "Annual WhatsApp API Pro Plan", 2400m);
        linkRes.IsSuccess.Should().BeTrue();
        linkRes.Value.Status.Should().Be(CheckoutStatus.Active);
        linkRes.Value.PaymentUrl.Should().Contain("https://pay.sgrowing.com");

        // Customer completes payment
        var paidRes = await service.MarkPaidAsync(tenantId, linkRes.Value.Id);
        paidRes.IsSuccess.Should().BeTrue();
        paidRes.Value.Status.Should().Be(CheckoutStatus.Paid);

        // Verify Won Deal created in CRM
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.ContactId == contact.Id && d.Stage == DealStage.Won);
        deal.Should().NotBeNull();
        deal!.Amount.Should().Be(2400m);

        // Verify contact is promoted to Customer
        var updatedContact = await db.Contacts.FindAsync(contact.Id);
        updatedContact!.LifecycleStage.Should().Be(LifecycleStage.Customer);
        updatedContact.Tags.Should().Contain("paying-customer");
    }

    [Fact]
    public async Task BookSlotAsync_WithInactiveOrMissingSlot_ReturnsFailure()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new AppointmentBookingService(db, notifier.Object);
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // 1. Missing slot
        var resMissing = await service.BookSlotAsync(tenantId, Guid.NewGuid(), contactId, DateTime.UtcNow.AddDays(1));
        resMissing.IsSuccess.Should().BeFalse();
        resMissing.Error.Should().Contain("Appointment slot not found or inactive");

        // 2. Inactive slot
        var inactiveSlot = AppointmentSlot.Create(tenantId, "Deprecated Onboarding", 30, "Inactive Rep");
        inactiveSlot.IsActive = false;
        db.AppointmentSlots.Add(inactiveSlot);
        await db.SaveChangesAsync();

        var resInactive = await service.BookSlotAsync(tenantId, inactiveSlot.Id, contactId, DateTime.UtcNow.AddDays(1));
        resInactive.IsSuccess.Should().BeFalse();
        resInactive.Error.Should().Contain("Appointment slot not found or inactive");
    }

    [Fact]
    public async Task BookSlotAsync_WhenContactMissing_StillCreatesBookingSuccessfully()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new AppointmentBookingService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var slot = AppointmentSlot.Create(tenantId, "Public Strategy Session", 30, "Open Calendar");
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        // Booking with a contact ID that does not exist in DB
        var nonExistentContactId = Guid.NewGuid();
        var res = await service.BookSlotAsync(tenantId, slot.Id, nonExistentContactId, DateTime.UtcNow.AddDays(3));

        res.IsSuccess.Should().BeTrue();
        res.Value.Status.Should().Be(BookingStatus.Confirmed);
        res.Value.ContactId.Should().Be(nonExistentContactId);
    }

    [Fact]
    public async Task CreateCheckoutLinkAsync_ShouldPersistCheckoutLinkWithCustomCurrency()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new CheckoutLinkService(db, notifier.Object);
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        var res = await service.CreateCheckoutLinkAsync(
            tenantId,
            contactId,
            convId,
            "Enterprise Dedicated IP",
            500m,
            currency: "EUR"
        );

        res.IsSuccess.Should().BeTrue();
        res.Value.Currency.Should().Be("EUR");
        res.Value.Amount.Should().Be(500m);
        res.Value.Status.Should().Be(CheckoutStatus.Active);
    }

    [Fact]
    public async Task MarkPaidAsync_WithNonExistentLink_ReturnsFailure()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new CheckoutLinkService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var res = await service.MarkPaidAsync(tenantId, Guid.NewGuid());

        res.IsSuccess.Should().BeFalse();
        res.Error.Should().Contain("Checkout link not found");
    }

    [Fact]
    public async Task MarkPaidAsync_ShouldPreventDuplicateTagAddition()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new CheckoutLinkService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Natasha", "Romanoff", "natasha@shield.gov");
        contact.Tags.Add("paying-customer"); // Already tagged!
        db.Contacts.Add(contact);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var linkRes = await service.CreateCheckoutLinkAsync(tenantId, contact.Id, conv.Id, "Shield Secure Tunnel", 1000m);
        var paidRes = await service.MarkPaidAsync(tenantId, linkRes.Value.Id);

        paidRes.IsSuccess.Should().BeTrue();

        var updatedContact = await db.Contacts.FindAsync(contact.Id);
        // Tag should appear exactly once
        updatedContact!.Tags.Count(t => t == "paying-customer").Should().Be(1);
    }
}
