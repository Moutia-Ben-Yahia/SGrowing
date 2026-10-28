using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.CartReservations;

public interface ICartReservationService
{
    Task<AcquireCartReservationResult> AcquireReservationAsync(Guid tenantId, AcquireCartReservationRequest req, CancellationToken ct = default);
    Task<bool> ReleaseReservationAsync(Guid tenantId, ReleaseCartReservationRequest req, CancellationToken ct = default);
    Task<bool> CommitReservationAsync(Guid tenantId, Guid cartId, Guid orderId, CancellationToken ct = default);
    Task<SweepExpiredReservationsResult> SweepExpiredReservationsAsync(Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<CartReservationLockRecord>> GetActiveReservationsAsync(Guid tenantId, Guid? warehouseId = null, string? sku = null, CancellationToken ct = default);
}
