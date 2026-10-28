using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.CartReservations;

public class CartReservationService : ICartReservationService
{
    private readonly SGrowingDbContext _db;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _skuLocks = new();

    public CartReservationService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<AcquireCartReservationResult> AcquireReservationAsync(Guid tenantId, AcquireCartReservationRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0)
        {
            return new AcquireCartReservationResult(
                ReservationId: Guid.Empty,
                CartId: req.CartId,
                Sku: req.Sku,
                Quantity: 0,
                Status: "Rejected",
                ExpiresAtUtc: DateTime.UtcNow,
                Success: false,
                Message: "Quantity must be greater than zero."
            );
        }

        string safeSku = req.Sku.Trim().ToUpperInvariant();
        string lockKey = $"{tenantId:N}:{req.WarehouseId:N}:{safeSku}";
        var sem = _skuLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

        await sem.WaitAsync(ct);
        try
        {
            var stock = await _db.WarehouseStockRecords
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.WarehouseId == req.WarehouseId && s.Sku == safeSku, ct);

            if (stock == null || stock.QuantityAvailable < req.Quantity)
            {
                return new AcquireCartReservationResult(
                    ReservationId: Guid.Empty,
                    CartId: req.CartId,
                    Sku: safeSku,
                    Quantity: req.Quantity,
                    Status: "InsufficientStock",
                    ExpiresAtUtc: DateTime.UtcNow,
                    Success: false,
                    Message: $"Insufficient available stock in warehouse for SKU '{safeSku}'. Available: {stock?.QuantityAvailable ?? 0}, Requested: {req.Quantity}."
                );
            }

            // Decrement available, increment reserved
            stock.QuantityAvailable -= req.Quantity;
            stock.QuantityReserved += req.Quantity;
            stock.RowVersion++;
            stock.UpdatedAtUtc = DateTime.UtcNow;

            int ttlMinutes = Math.Clamp(req.TtlMinutes, 1, 120);
            var expiresAt = DateTime.UtcNow.AddMinutes(ttlMinutes);

            var lockRecord = new CartReservationLockRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WarehouseId = req.WarehouseId,
                Sku = safeSku,
                Quantity = req.Quantity,
                CartId = req.CartId,
                Status = "Active",
                ExpiresAtUtc = expiresAt,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.CartReservationLocks.Add(lockRecord);
            await _db.SaveChangesAsync(ct);
            SGrowingTelemetry.CartReservationsAcquiredCounter.Add(req.Quantity);

            return new AcquireCartReservationResult(
                ReservationId: lockRecord.Id,
                CartId: req.CartId,
                Sku: safeSku,
                Quantity: req.Quantity,
                Status: "Active",
                ExpiresAtUtc: expiresAt,
                Success: true,
                Message: $"Successfully reserved {req.Quantity} unit(s) of '{safeSku}' for cart {req.CartId}. Expires in {ttlMinutes} minutes."
            );
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<bool> ReleaseReservationAsync(Guid tenantId, ReleaseCartReservationRequest req, CancellationToken ct = default)
    {
        var activeLocks = await _db.CartReservationLocks
            .Where(l => l.TenantId == tenantId && l.CartId == req.CartId && l.Status == "Active")
            .ToListAsync(ct);

        if (!activeLocks.Any()) return false;

        foreach (var lockRec in activeLocks)
        {
            var stock = await _db.WarehouseStockRecords
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.WarehouseId == lockRec.WarehouseId && s.Sku == lockRec.Sku, ct);

            if (stock != null)
            {
                stock.QuantityReserved = Math.Max(0, stock.QuantityReserved - lockRec.Quantity);
                stock.QuantityAvailable += lockRec.Quantity;
                stock.RowVersion++;
                stock.UpdatedAtUtc = DateTime.UtcNow;
            }

            lockRec.Status = "Released";
            lockRec.ReleasedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CommitReservationAsync(Guid tenantId, Guid cartId, Guid orderId, CancellationToken ct = default)
    {
        var activeLocks = await _db.CartReservationLocks
            .Where(l => l.TenantId == tenantId && l.CartId == cartId && l.Status == "Active")
            .ToListAsync(ct);

        if (!activeLocks.Any()) return false;

        foreach (var lockRec in activeLocks)
        {
            lockRec.Status = "Committed";
            lockRec.OrderId = orderId;
            lockRec.ReleasedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SweepExpiredReservationsResult> SweepExpiredReservationsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiredLocks = await _db.CartReservationLocks
            .Where(l => l.TenantId == tenantId && l.Status == "Active" && l.ExpiresAtUtc < now)
            .ToListAsync(ct);

        int stockReturned = 0;

        foreach (var lockRec in expiredLocks)
        {
            var stock = await _db.WarehouseStockRecords
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.WarehouseId == lockRec.WarehouseId && s.Sku == lockRec.Sku, ct);

            if (stock != null)
            {
                stock.QuantityReserved = Math.Max(0, stock.QuantityReserved - lockRec.Quantity);
                stock.QuantityAvailable += lockRec.Quantity;
                stock.RowVersion++;
                stock.UpdatedAtUtc = DateTime.UtcNow;
                stockReturned += lockRec.Quantity;
            }

            lockRec.Status = "Expired";
            lockRec.ReleasedAtUtc = DateTime.UtcNow;
        }

        if (expiredLocks.Any())
        {
            await _db.SaveChangesAsync(ct);
        }

        return new SweepExpiredReservationsResult(
            TotalExpiredFound: expiredLocks.Count,
            StockReturnedCount: stockReturned,
            SweptAtUtc: DateTime.UtcNow
        );
    }

    public async Task<IEnumerable<CartReservationLockRecord>> GetActiveReservationsAsync(Guid tenantId, Guid? warehouseId = null, string? sku = null, CancellationToken ct = default)
    {
        var query = _db.CartReservationLocks
            .Where(l => l.TenantId == tenantId && l.Status == "Active");

        if (warehouseId.HasValue)
        {
            query = query.Where(l => l.WarehouseId == warehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(sku))
        {
            string safeSku = sku.Trim().ToUpperInvariant();
            query = query.Where(l => l.Sku == safeSku);
        }

        return await query.OrderByDescending(l => l.CreatedAtUtc).ToListAsync(ct);
    }
}
