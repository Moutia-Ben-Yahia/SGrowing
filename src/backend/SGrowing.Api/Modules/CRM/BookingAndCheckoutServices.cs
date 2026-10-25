using SGrowing.Api.Data;
using SGrowing.Api.Services;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.CRM;

public class AppointmentBookingService
{
    private readonly SGrowingDbContext _db;
    private readonly IRealtimeNotifier _notifier;

    public AppointmentBookingService(SGrowingDbContext db, IRealtimeNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    public async Task<Result<AppointmentBooking>> BookSlotAsync(
        Guid tenantId,
        Guid slotId,
        Guid contactId,
        DateTime scheduledTimeUtc,
        CancellationToken cancellationToken = default)
    {
        var slot = await _db.AppointmentSlots
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == slotId && s.IsActive, cancellationToken);

        if (slot == null)
            return Result<AppointmentBooking>.Failure("Appointment slot not found or inactive.");

        var booking = AppointmentBooking.Create(tenantId, slotId, contactId, scheduledTimeUtc);
        _db.AppointmentBookings.Add(booking);

        // Advance contact lifecycle to Lead / Opportunity
        var contact = await _db.Contacts.FindAsync(new object[] { contactId }, cancellationToken);
        if (contact != null)
        {
            contact.LifecycleStage = LifecycleStage.Lead;
            if (!contact.Tags.Contains("demo-scheduled"))
                contact.Tags.Add("demo-scheduled");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<AppointmentBooking>.Success(booking);
    }
}

public class CheckoutLinkService
{
    private readonly SGrowingDbContext _db;
    private readonly IRealtimeNotifier _notifier;

    public CheckoutLinkService(SGrowingDbContext db, IRealtimeNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    public async Task<Result<CheckoutLink>> CreateCheckoutLinkAsync(
        Guid tenantId,
        Guid contactId,
        Guid conversationId,
        string title,
        decimal amount,
        string currency = "USD",
        CancellationToken cancellationToken = default)
    {
        var link = CheckoutLink.Create(tenantId, contactId, conversationId, title, amount, currency);
        _db.CheckoutLinks.Add(link);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<CheckoutLink>.Success(link);
    }

    public async Task<Result<CheckoutLink>> MarkPaidAsync(
        Guid tenantId,
        Guid checkoutLinkId,
        CancellationToken cancellationToken = default)
    {
        var link = await _db.CheckoutLinks
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == checkoutLinkId, cancellationToken);

        if (link == null)
            return Result<CheckoutLink>.Failure("Checkout link not found.");

        link.MarkPaid();

        // Create or update Won Deal in CRM
        var deal = Deal.Create(tenantId, link.ContactId, $"Paid: {link.Title}", link.Amount, DealStage.Won);
        deal.Notes = $"Assisted commerce payment completed via {link.PaymentUrl}";
        _db.Deals.Add(deal);

        // Update contact lifecycle stage to Customer
        var contact = await _db.Contacts.FindAsync(new object[] { link.ContactId }, cancellationToken);
        if (contact != null)
        {
            contact.LifecycleStage = LifecycleStage.Customer;
            if (!contact.Tags.Contains("paying-customer"))
                contact.Tags.Add("paying-customer");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<CheckoutLink>.Success(link);
    }
}
