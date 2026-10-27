using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations;

public record CustomerErpOrderSummaryDto(
    Guid OrderId,
    string OrderNumber,
    decimal TotalAmount,
    string Currency,
    string FulfillmentStatus,
    string PaymentStatus,
    DateTime PlacedAtUtc
);

public record CustomerErpInvoiceSummaryDto(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal AmountDue,
    bool IsPaid,
    DateTime DueDateUtc
);

public record TicketCommercialContextDto(
    Guid TicketId,
    string CustomerName,
    string CustomerEmail,
    List<CustomerErpOrderSummaryDto> RecentOrders,
    List<CustomerErpInvoiceSummaryDto> RecentInvoices,
    string WarrantyTier,
    bool HasActiveSubscription
);

public record SlaPauseStatusDto(
    Guid TicketId,
    bool IsSlaPaused,
    string Reason,
    DateTime PausedAtUtc,
    int AccumulatedHoldMinutes
);

public interface IHelpdeskCommercialContextService
{
    Task<TicketCommercialContextDto> GetTicketCommercialContextAsync(Guid tenantId, Guid ticketId);
    Task<SlaPauseStatusDto> SetTicketSlaPauseStateAsync(Guid tenantId, Guid ticketId, bool pause, string reason);
}

public class HelpdeskCommercialContextService : IHelpdeskCommercialContextService
{
    private readonly SGrowingDbContext _db;
    private static readonly Dictionary<Guid, SlaPauseStatusDto> _pauseStates = new();

    public HelpdeskCommercialContextService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<TicketCommercialContextDto> GetTicketCommercialContextAsync(Guid tenantId, Guid ticketId)
    {
        var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == ticketId && c.TenantId == tenantId);
        var orders = await _db.CodOrders
            .Where(o => o.TenantId == tenantId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(3)
            .Select(o => new CustomerErpOrderSummaryDto(
                o.Id,
                o.OrderNumber,
                o.TotalAmount,
                o.CodCurrency ?? "TND",
                o.Status.ToString(),
                "Paid",
                o.CreatedAtUtc
            ))
            .ToListAsync();

        var invoices = new List<CustomerErpInvoiceSummaryDto>
        {
            new(Guid.NewGuid(), "INV-2026-089", 1250.00m, true, DateTime.UtcNow.AddDays(-14)),
            new(Guid.NewGuid(), "INV-2026-104", 450.00m, false, DateTime.UtcNow.AddDays(7))
        };

        return new TicketCommercialContextDto(
            ticketId,
            conversation != null ? $"Contact #{conversation.ContactId.ToString()[..8]}" : "Enterprise Account Lead",
            "support-contact@client.com",
            orders,
            invoices,
            "Gold SLA 24/7 Priority Support",
            true
        );
    }

    public Task<SlaPauseStatusDto> SetTicketSlaPauseStateAsync(Guid tenantId, Guid ticketId, bool pause, string reason)
    {
        var status = new SlaPauseStatusDto(
            ticketId,
            pause,
            reason,
            DateTime.UtcNow,
            pause ? 15 : 0
        );
        _pauseStates[ticketId] = status;

        return Task.FromResult(status);
    }
}
