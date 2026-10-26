using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Channels;

public interface ILiveCommerceOverlayService
{
    Task<LiveCommentOrderResult> ProcessLiveCommentAsync(
        Guid tenantId,
        Guid sessionId,
        string commenterUsername,
        string commentText,
        string? customerPhone = null,
        string? governorate = null);

    Task<List<LiveOverlayStockDto>> GetLiveOverlayStockFeedAsync(Guid tenantId, Guid sessionId);
}

public class LiveCommerceOverlayService : ILiveCommerceOverlayService
{
    private readonly SGrowingDbContext _db;

    public LiveCommerceOverlayService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<LiveCommentOrderResult> ProcessLiveCommentAsync(
        Guid tenantId,
        Guid sessionId,
        string commenterUsername,
        string commentText,
        string? customerPhone = null,
        string? governorate = null)
    {
        var session = await _db.LiveStreamSessions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sessionId);

        if (session == null)
            throw new InvalidOperationException($"Live session {sessionId} not found.");

        session.TotalCommentsProcessed++;

        var activeDeals = await _db.LiveFlashDeals
            .Where(d => d.TenantId == tenantId && d.SessionId == sessionId && d.IsActive)
            .ToListAsync();

        var text = commentText.Trim().ToUpperInvariant();
        LiveFlashDeal? matchedDeal = null;

        foreach (var deal in activeDeals)
        {
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(deal.TriggerKeyword.ToUpperInvariant())}\b"))
            {
                matchedDeal = deal;
                break;
            }
        }

        if (matchedDeal == null)
        {
            await _db.SaveChangesAsync();
            return new LiveCommentOrderResult(
                Success: false,
                OrderId: null,
                CustomerUsername: commenterUsername,
                Sku: string.Empty,
                Price: 0m,
                Message: "Comment did not match any active flash deal trigger keyword.",
                CheckoutCardJson: "{}");
        }

        if (matchedDeal.RemainingQuantity <= 0)
        {
            await _db.SaveChangesAsync();
            return new LiveCommentOrderResult(
                Success: false,
                OrderId: null,
                CustomerUsername: commenterUsername,
                Sku: matchedDeal.Sku,
                Price: matchedDeal.FlashPrice,
                Message: $"Flash deal for {matchedDeal.ProductName} is SOLD OUT!",
                CheckoutCardJson: "{}");
        }

        // Deal matched and stock available
        matchedDeal.SoldQuantity++;
        session.TotalOrdersGenerated++;

        var phone = string.IsNullOrWhiteSpace(customerPhone) ? "+21620000000" : customerPhone;
        var gov = string.IsNullOrWhiteSpace(governorate) ? "Tunis" : governorate;
        var orderNum = $"LIVE-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";

        var itemsJson = JsonSerializer.Serialize(new[]
        {
            new { sku = matchedDeal.Sku, title = matchedDeal.ProductName, quantity = 1, unitPrice = matchedDeal.FlashPrice }
        });

        var order = CodOrder.Create(
            tenantId: tenantId,
            orderNumber: orderNum,
            customerName: commenterUsername,
            phoneNumber: phone,
            shippingAddress: "Adresse à confirmer via Live DM",
            governorateOrWilaya: gov,
            delegationOrCommune: gov,
            totalAmount: matchedDeal.FlashPrice + 7.0m, // flash price + 7 DT shipping
            itemsJson: itemsJson,
            notes: $"Generated from Live Stream '{session.Title}' (Trigger: {matchedDeal.TriggerKeyword})");

        order.Status = CodOrderStatus.PendingConfirmation;
        _db.CodOrders.Add(order);

        // Defensive WMS: Reserve inventory
        var inventories = await _db.WarehouseInventories.Where(w => w.TenantId == tenantId).ToListAsync();
        var inv = inventories.FirstOrDefault(i => i.Sku == matchedDeal.Sku) ?? inventories.FirstOrDefault();
        if (inv != null)
        {
            inv.ReservedStock += 1;
            inv.LastUpdatedUtc = DateTime.UtcNow;
        }

        var card = JsonSerializer.Serialize(new
        {
            type = "live_stream_checkout",
            orderNumber = order.OrderNumber,
            customer = commenterUsername,
            product = matchedDeal.ProductName,
            flashPrice = matchedDeal.FlashPrice,
            shipping = 7.0m,
            total = order.TotalAmount,
            ctaUrl = $"https://store.sgrowing.com/confirm/{order.Id:N}"
        });

        await _db.SaveChangesAsync();

        return new LiveCommentOrderResult(
            Success: true,
            OrderId: order.Id,
            CustomerUsername: commenterUsername,
            Sku: matchedDeal.Sku,
            Price: matchedDeal.FlashPrice,
            Message: $"Order #{order.OrderNumber} created for @{commenterUsername}! 1-Click DM checkout dispatched.",
            CheckoutCardJson: card);
    }

    public async Task<List<LiveOverlayStockDto>> GetLiveOverlayStockFeedAsync(Guid tenantId, Guid sessionId)
    {
        var deals = await _db.LiveFlashDeals
            .Where(d => d.TenantId == tenantId && d.SessionId == sessionId && d.IsActive)
            .ToListAsync();

        return deals.Select(d => new LiveOverlayStockDto(
            ProductName: d.ProductName,
            Sku: d.Sku,
            FlashPrice: d.FlashPrice,
            RemainingUnits: d.RemainingQuantity,
            TriggerKeyword: d.TriggerKeyword,
            IsUrgentAlert: d.RemainingQuantity <= 5)).ToList();
    }
}
