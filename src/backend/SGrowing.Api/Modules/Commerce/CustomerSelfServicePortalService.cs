using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface ICustomerSelfServicePortalService
{
    Task<PublicTrackingResult> LookupTrackingAsync(string orderNumber, string phone);
    Task<ReturnMerchandiseAuthorization> SubmitRmaRequestAsync(Guid tenantId, string orderNumber, string phone, RmaReason reason, string? notes, List<string>? photoUrls = null);
    Task<ReturnMerchandiseAuthorization> ReviewRmaRequestAsync(Guid tenantId, Guid rmaId, bool isApproved, string resolutionNotes, bool dispatchReverseCourier = false, Guid? courierId = null);
}

public record PublicTrackingResult(
    string OrderNumber,
    string CustomerNameMasked,
    string Status,
    string? CourierName,
    string? TrackingNumber,
    string? TrackingUrl,
    decimal TotalAmount,
    string Currency,
    List<string> TrackingTimeline);

public class CustomerSelfServicePortalService : ICustomerSelfServicePortalService
{
    private readonly SGrowingDbContext _dbContext;
    private readonly ICourierLogisticsHubService _courierHub;

    public CustomerSelfServicePortalService(SGrowingDbContext dbContext, ICourierLogisticsHubService courierHub)
    {
        _dbContext = dbContext;
        _courierHub = courierHub;
    }

    private static string MaskName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Customer";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(p => p.Length > 2 ? p[0] + new string('*', p.Length - 2) + p[^1] : p[0] + "*"));
    }

    public async Task<PublicTrackingResult> LookupTrackingAsync(string orderNumber, string phone)
    {
        var cleanPhoneDigits = Regex.Replace(phone, @"\D", "");

        var order = await _dbContext.CodOrders
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber.Trim());

        if (order == null)
            throw new InvalidOperationException($"Order #{orderNumber} not found.");

        var orderPhoneDigits = Regex.Replace(order.PhoneNumber, @"\D", "");

        // Authenticate by phone: must match full digits or last 4 digits
        bool isPhoneMatch = orderPhoneDigits.EndsWith(cleanPhoneDigits) || cleanPhoneDigits.EndsWith(orderPhoneDigits);
        if (!isPhoneMatch)
            throw new UnauthorizedAccessException("Phone number does not match order records.");

        var shipment = await _dbContext.ShipmentRecords
            .FirstOrDefaultAsync(s => s.TenantId == order.TenantId && s.OrderId == order.Id);

        var timeline = new List<string>
        {
            $"Order Placed ({order.CreatedAtUtc:yyyy-MM-dd HH:mm} UTC)"
        };

        if (order.ConfirmedAtUtc.HasValue)
        {
            timeline.Add($"Order Confirmed via {order.ConfirmationChannel} ({order.ConfirmedAtUtc.Value:yyyy-MM-dd HH:mm} UTC)");
        }

        if (shipment != null)
        {
            timeline.Add($"Dispatched via {shipment.CourierName} - Tracking #{shipment.TrackingNumber} ({shipment.DispatchedAtUtc:yyyy-MM-dd HH:mm} UTC)");
            if (shipment.Status == CourierShipmentStatus.OutForDelivery)
            {
                timeline.Add("Package is Out for Delivery with courier agent.");
            }
            else if (shipment.Status == CourierShipmentStatus.Delivered)
            {
                timeline.Add($"Delivered successfully ({shipment.DeliveredAtUtc:yyyy-MM-dd HH:mm} UTC)");
            }
        }

        return new PublicTrackingResult(
            order.OrderNumber,
            MaskName(order.CustomerName),
            order.Status.ToString(),
            shipment?.CourierName,
            shipment?.TrackingNumber,
            shipment != null ? $"https://tracking.sgrowing.io/{shipment.TrackingNumber}" : null,
            order.TotalAmount + order.ShippingFee,
            order.CodCurrency,
            timeline);
    }

    public async Task<ReturnMerchandiseAuthorization> SubmitRmaRequestAsync(Guid tenantId, string orderNumber, string phone, RmaReason reason, string? notes, List<string>? photoUrls = null)
    {
        var order = await _dbContext.CodOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.OrderNumber == orderNumber.Trim())
            ?? throw new InvalidOperationException($"Order #{orderNumber} not found.");

        var cleanPhoneDigits = Regex.Replace(phone, @"\D", "");
        var orderPhoneDigits = Regex.Replace(order.PhoneNumber, @"\D", "");

        if (!orderPhoneDigits.EndsWith(cleanPhoneDigits) && !cleanPhoneDigits.EndsWith(orderPhoneDigits))
            throw new UnauthorizedAccessException("Phone number verification failed.");

        var rmaNumber = $"RMA-{order.OrderNumber}-{Random.Shared.Next(100, 999)}";

        var rma = new ReturnMerchandiseAuthorization
        {
            TenantId = tenantId,
            RmaNumber = rmaNumber,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerPhone = order.PhoneNumber,
            Status = RmaStatus.Requested,
            Reason = reason,
            CustomerNotes = notes,
            PhotoEvidenceUrlsJson = JsonSerializer.Serialize(photoUrls ?? new List<string>()),
            RequestedAtUtc = DateTime.UtcNow
        };

        _dbContext.ReturnMerchandiseAuthorizations.Add(rma);
        await _dbContext.SaveChangesAsync();

        return rma;
    }

    public async Task<ReturnMerchandiseAuthorization> ReviewRmaRequestAsync(Guid tenantId, Guid rmaId, bool isApproved, string resolutionNotes, bool dispatchReverseCourier = false, Guid? courierId = null)
    {
        var rma = await _dbContext.ReturnMerchandiseAuthorizations
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == rmaId)
            ?? throw new InvalidOperationException($"RMA {rmaId} not found.");

        rma.Status = isApproved ? RmaStatus.Approved : RmaStatus.Rejected;
        rma.ResolutionNotes = resolutionNotes;
        rma.ResolvedAtUtc = DateTime.UtcNow;

        if (isApproved && dispatchReverseCourier && courierId.HasValue)
        {
            rma.Status = RmaStatus.ReverseCourierDispatched;
            // Dispatch reverse shipment through logistics hub
            await _courierHub.DispatchOrderAsync(tenantId, rma.OrderId, courierId.Value);
        }

        await _dbContext.SaveChangesAsync();
        return rma;
    }
}
