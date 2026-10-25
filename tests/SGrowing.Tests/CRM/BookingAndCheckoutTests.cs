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
        tenantContext.SetTenant(Guid.NewGuid());
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
}
